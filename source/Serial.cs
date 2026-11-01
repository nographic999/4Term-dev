/* Serial.cs -- the serial port connection: opening and closing the port, a
   background thread that reads incoming data, sending, and the DTR and RTS
   lines.

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

using System;
using System.IO;
using System.Threading;

namespace _4Term
{
    /* The serial port connection, shared through Instance.  */
    public sealed class Serial
    {
        /* Timing constants, in milliseconds.  */

        /* How long a read waits for the first byte before the reader checks
           the port again.  A read returns as soon as any byte arrives, so this
           adds no delay.  */
        private const int ReadTimeout = 50;

        /* Longest wait for a write, which covers a device holding off with
           handshaking.  */
        private const int WriteTimeout = 250;

        /* How often an idle reader checks that the port still exists, to
           notice a USB unplug.  */
        private const int PortCheckInterval = 500;

        /* Longest wait for the reading thread to finish when the port
           closes.  */
        private const int ThreadJoinTimeout = 1000;

        /* The underlying serial port.  */
        private readonly System.IO.Ports.SerialPort SerialPortInstance;

        /* The one Serial object, shared by the whole program.  */
        public static Serial Instance => instance;

        /* Backing field for Instance.  */
        static readonly Serial instance = new Serial();
        
        /* True while the port is open.  */
        public bool IsOpen => SerialPortInstance.IsOpen;

        /* True while the reading thread should keep running; clearing it asks
           the thread to stop.  */
        private volatile bool ReadThreadFlag = false;      

        /* Events.  */

        /* Signature of the Serial events; PARAM is the status text or the
           received data.  */
        public delegate void SerialEventHandler(string param);

        /* Raised with a status line when the port opens, closes or fails.  */
        public event SerialEventHandler StatusChanged;

        /* Raised on the reading thread when data arrives.  The string carries
           raw bytes, one char per byte (0-255), not decoded text.  */
        public event SerialEventHandler DataReceived;

        /* Latin-1 maps every byte 0-255 to the char with the same code, so
           received data reaches the UI without losing bytes 0x80 and
           above.  */
        public static System.Text.Encoding RawEncoding => System.Text.Encoding.GetEncoding(28591);

        /* The thread running ReadThread, or null when the port is closed.  */
        private Thread ReadThreadInstance = null;         

        /* Create the one Serial object.  The port uses RawEncoding so that no
           byte is altered by text conversion.  */
        private Serial()
        {
            SerialPortInstance = new System.IO.Ports.SerialPort
            {
                Encoding = RawEncoding
            };
        }

        /* Open the port with the settings in Settings.Port and start the
           reading thread.  A port that is already open is closed first.  The
           result, or the error, is reported through StatusChanged.  */
        public void Open()
        {
            /* Make sure no previous reading thread or connection is left
               over.  */
            StopReadThread();
            if (SerialPortInstance.IsOpen)
                ClosePortQuietly();

            try
            {
                /* Apply the port settings.  */
                SerialPortInstance.PortName = Settings.Port.PortName;
                SerialPortInstance.BaudRate = Settings.Port.BaudRate;
                SerialPortInstance.Parity = Settings.Port.Parity;
                SerialPortInstance.DataBits = Settings.Port.DataBits;
                SerialPortInstance.StopBits = Settings.Port.StopBits;
                SerialPortInstance.Handshake = Settings.Port.Handshake;
                SerialPortInstance.ReadTimeout = ReadTimeout;
                SerialPortInstance.WriteTimeout = WriteTimeout;
                SerialPortInstance.DtrEnable = Settings.Port.Dtr;

                /* Under RTS/CTS handshaking the handshake drives RTS, and
                   .NET refuses to have it set by hand.  */
                if (!RtsUsedByHandshake)
                    SerialPortInstance.RtsEnable = Settings.Port.Rts;

                /* Open the port.  */
                SerialPortInstance.Open();

                /* Work around a .NET Framework bug: after a USB serial device
                   is unplugged, the stream's finalizer fails on the finalizer
                   thread and ends the whole program.  The stream is closed
                   explicitly by Close instead, which tolerates the missing
                   device.  */
                GC.SuppressFinalize(SerialPortInstance.BaseStream);

                /* Start the reading thread.  As a background thread it never
                   keeps the process alive.  */
                ReadThreadFlag = true;
                ReadThreadInstance = new Thread(ReadThread)
                {
                    IsBackground = true,
                    Name = "4Term serial reader"
                };
                ReadThreadInstance.Start();

                /* Report the connection, for example "Connected to COM3:
                   115200, 8N1, no handshake".  */
                string Parity = SerialPortInstance.Parity.ToString().Substring(0, 1);
                string StopBits = SerialPortInstance.StopBits == System.IO.Ports.StopBits.OnePointFive
                    ? "1.5"
                    : ((int)SerialPortInstance.StopBits).ToString();
                string Handshake = SerialPortInstance.Handshake.ToString();
                if (SerialPortInstance.Handshake == System.IO.Ports.Handshake.None)
                    Handshake = "no handshake";

                StatusChanged?.Invoke($"Connected to {SerialPortInstance.PortName}: " +
                    $"{SerialPortInstance.BaudRate}, " +
                    $"{SerialPortInstance.DataBits}{Parity}{StopBits}, " +
                    $"{Handshake}");
            }

            /* Opening failed: clean up and report the error.  */
            catch (Exception ex)
            {
                ReadThreadFlag = false;
                ReadThreadInstance = null;
                ClosePortQuietly();
                StatusChanged?.Invoke($"Error: {ex.Message}");
            }
        }

        /* Body of the reading thread.  Block in Read until bytes arrive, so
           data is handled within a millisecond or two with no CPU use while
           idle, and pass each chunk to DataReceived.

           Any port failure, including a USB unplug, counts as a disconnect:
           the port is closed so that IsOpen becomes false and auto-connect can
           reopen it.  */
        private void ReadThread()
        {
            int lastPortCheck = Environment.TickCount;
            byte[] buffer = new byte[4096];

            while (ReadThreadFlag)
            {
                try
                {
                    /* The port was closed underneath the thread.  */
                    if (!SerialPortInstance.IsOpen)
                    {
                        if (ReadThreadFlag)
                        {
                            ReadThreadFlag = false;
                            StatusChanged?.Invoke("Disconnected");
                        }
                        break;
                    }

                    /* Wait for data; Read returns as soon as any bytes
                       arrive.  */
                    int count = 0;
                    bool timedOut = false;
                    try
                    {
                        count = SerialPortInstance.BaseStream.Read(buffer, 0, buffer.Length);
                    }
                    catch (TimeoutException)
                    {
                        timedOut = true;
                    }

                    /* Pass the data on, one char per byte.  */
                    if (count > 0)
                    {
                        DataReceived?.Invoke(RawEncoding.GetString(buffer, 0, count));
                        continue;
                    }

                    /* Nothing read.  A read that returns nothing at once,
                       which some drivers do once the device is gone, must
                       not make the loop spin.  */
                    if (!timedOut)
                        Thread.Sleep(ReadTimeout);

                    /* Idle: now and then, check that the port still
                       exists.  */
                    if (unchecked(Environment.TickCount - lastPortCheck) >= PortCheckInterval)
                    {
                        lastPortCheck = Environment.TickCount;
                        if (!PortStillPresent(SerialPortInstance.PortName))
                            throw new IOException($"{SerialPortInstance.PortName} was removed");
                    }
                }

                /* Any failure while reading ends the connection.  */
                catch (Exception ex)
                {
                    /* Expected while Close is shutting the port down.  */
                    if (!ReadThreadFlag)
                        break;

                    ReadThreadFlag = false;
                    ClosePortQuietly();
                    StatusChanged?.Invoke($"Error: {ex.Message}");
                }
            }
        }

        /* Send DATA followed by the line ending chosen in the settings (none,
           CR, LF or CR LF), encoded as UTF-8.  Return the line ending that was
           added, or null if nothing could be sent; the reason is then reported
           through StatusChanged.  A write timeout keeps the connection; any
           other failure closes it.  */
        public string Send(string data)
        {
            string LineEnding = "";

            /* Choose the line ending from the settings.  */
            switch (Settings.Port.Append)
            {
                /* Carriage return.  */
                case Settings.Port.AppendType.AppendCR:
                    LineEnding = "\r";
                    break;

                /* Line feed.  */
                case Settings.Port.AppendType.AppendLF:
                    LineEnding = "\n";
                    break;

                /* Carriage return and line feed.  */
                case Settings.Port.AppendType.AppendCRLF:
                    LineEnding = "\r\n";
                    break;
            }

            if (!SerialPortInstance.IsOpen)
            {
                StatusChanged?.Invoke("Error: The serial port is not open");
                return null;
            }

            try
            {
                /* Send DATA and the line ending as UTF-8.  */
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(data + LineEnding);
                SerialPortInstance.Write(bytes, 0, bytes.Length);
            }

            /* The device is not accepting data, for example because
               handshaking holds it off; keep the connection.  */
            catch (TimeoutException)
            {
                StatusChanged?.Invoke("Error: Write timeout, device is not accepting data");
                return null;
            }

            /* Port failure, for example a USB unplug: treat it as a
               disconnect.  */
            catch (Exception ex)
            {
                Close();
                StatusChanged?.Invoke($"Error: {ex.Message}");
                return null;
            }

            /* Return the line ending so the caller can show it.  */
            return LineEnding;
        }

        /* Stop the reading thread, close the port and report
           "Disconnected".  */
        public void Close()
        {
            StopReadThread();
            ClosePortQuietly();
            StatusChanged?.Invoke("Disconnected");
        }

        /* Ask the reading thread to stop and wait up to ThreadJoinTimeout for
           it to finish.  The thread blocks for at most ReadTimeout and its
           handlers never wait for the UI, so it normally ends at once; if it
           is still running, closing the port ends it.  */
        private void StopReadThread()
        {
            ReadThreadFlag = false;

            Thread thread = ReadThreadInstance;
            if (thread != null && thread.IsAlive && thread != Thread.CurrentThread)
                thread.Join(ThreadJoinTimeout);

            ReadThreadInstance = null;
        }

        /* Discard the buffers and close the port, ignoring errors from a
           device that has already gone away.  */
        private void ClosePortQuietly()
        {
            try
            {
                if (SerialPortInstance.IsOpen)
                {
                    SerialPortInstance.DiscardInBuffer();
                    SerialPortInstance.DiscardOutBuffer();
                }
            }
            catch { }

            try
            {
                SerialPortInstance.Close();
            }
            catch { }
        }

        /* Return true if the system still lists the port PORTNAME.  Tolerate
           the known GetPortNames quirk of extra characters after the name, and
           assume the port is present if the list cannot be read.  */
        private static bool PortStillPresent(string portName)
        {
            try
            {
                foreach (string name in System.IO.Ports.SerialPort.GetPortNames())
                {
                    if (name.StartsWith(portName, StringComparison.OrdinalIgnoreCase) &&
                        (name.Length == portName.Length || !char.IsDigit(name[portName.Length])))
                        return true;
                }
                return false;
            }
            catch
            {
                return true;
            }
        }

        /* True if the handshake setting of the port lets RTS/CTS
           handshaking drive the RTS line, so that it cannot be set by
           hand.  */
        public bool RtsUsedByHandshake =>
            SerialPortInstance.Handshake == System.IO.Ports.Handshake.RequestToSend ||
            SerialPortInstance.Handshake == System.IO.Ports.Handshake.RequestToSendXOnXOff;

        /* Set the DTR line to ENABLED.  Do nothing if the port is not open.
           DTR is often used to reset or wake a device, and some USB boards
           only send output while it is on.  */
        public void SetDtr(bool enabled)
        {
            if (SerialPortInstance.IsOpen)
                SerialPortInstance.DtrEnable = enabled;
        }

        /* Set the RTS line to ENABLED.  Return false, changing nothing, if
           the port is not open or RTS/CTS handshaking drives the line.  RTS
           is used for hardware flow control, and on many boards to reset the
           device or select its boot mode.  */
        public bool SetRts(bool enabled)
        {
            if (!SerialPortInstance.IsOpen || RtsUsedByHandshake)
                return false;

            SerialPortInstance.RtsEnable = enabled;
            return true;
        }
    }
}
