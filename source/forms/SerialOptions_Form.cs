/* SerialOptions_Form.cs -- the serial options dialog: port, baud rate, data
   format, handshaking, line ending and the DTR and RTS lines.

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Windows.Forms;

namespace _4Term
{
    /* The serial options dialog.  */
    public partial class SerialPortOptionsForm : Form
    {
        /* Refreshes the port list while it is open.  */
        private Timer comPortMonitorTimer;
        /* Port names at the last check.  */
        private string[] lastKnownPorts = Array.Empty<string>();

        /* The radio button groups and the setting value of each button.  */
        private readonly Choice<int> BaudRateChoice;
        private readonly Choice<int> DataBitsChoice;
        private readonly Choice<Parity> ParityChoice;
        private readonly Choice<StopBits> StopBitsChoice;
        private readonly Choice<Handshake> HandshakeChoice;
        private readonly Choice<Settings.Port.AppendType> AppendChoice;

        /* A group of radio buttons, each standing for one setting value.  */
        private sealed class Choice<T>
        {
            private readonly RadioButton[] Buttons;
            private readonly T[] Values;

            /* Pair BUTTONS with VALUES, in the same order.  */
            public Choice(RadioButton[] buttons, T[] values)
            {
                Buttons = buttons;
                Values = values;
            }

            /* Check the button for VALUE.  Return false, checking nothing, if
               no button stands for VALUE.  */
            public bool Show(T value)
            {
                int i = Array.IndexOf(Values, value);
                if (i < 0)
                    return false;

                Buttons[i].Checked = true;
                return true;
            }

            /* Return the value of the checked button, or FALLBACK if none is
               checked.  */
            public T Selected(T fallback)
            {
                for (int i = 0; i < Buttons.Length; i++)
                    if (Buttons[i].Checked)
                        return Values[i];

                return fallback;
            }
        }

        /* Create the dialog and show the saved settings in it.  */
        public SerialPortOptionsForm()
        {
            InitializeComponent();
            comPortMonitorTimer = new Timer();
            comPortMonitorTimer.Interval = 250;
            comPortMonitorTimer.Tick += ComPortMonitorTimer_Tick;
            FormClosed += (sender, e) => comPortMonitorTimer.Dispose();

            /* Fill the port list.  */
            COMPortComboBox_Click(this, EventArgs.Empty);

            BaudRateChoice = new Choice<int>(
                new[] { BaudRate600RadioButton, BaudRate1200RadioButton, BaudRate2400RadioButton,
                        BaudRate4800RadioButton, BaudRate9600RadioButton, BaudRate14400RadioButton,
                        BaudRate19200RadioButton, BaudRate28800RadioButton, BaudRate38400RadioButton,
                        BaudRate56000RadioButton, BaudRate57600RadioButton, BaudRate115200RadioButton,
                        BaudRate128000RadioButton, BaudRate256000RadioButton },
                new[] { 600, 1200, 2400, 4800, 9600, 14400, 19200, 28800, 38400,
                        56000, 57600, 115200, 128000, 256000 });
            DataBitsChoice = new Choice<int>(
                new[] { DataBits5RadioButton, DataBits6RadioButton, DataBits7RadioButton, DataBits8RadioButton },
                new[] { 5, 6, 7, 8 });
            ParityChoice = new Choice<Parity>(
                new[] { ParityNoneRadioButton, ParityOddRadioButton, ParityEvenRadioButton,
                        ParityMarkRadioButton, ParitySpaceRadioButton },
                new[] { Parity.None, Parity.Odd, Parity.Even, Parity.Mark, Parity.Space });
            StopBitsChoice = new Choice<StopBits>(
                new[] { StopBits1RadioButton, StopBits15RadioButton, StopBits2RadioButton },
                new[] { StopBits.One, StopBits.OnePointFive, StopBits.Two });
            HandshakeChoice = new Choice<Handshake>(
                new[] { HandshakingNoneRadioButton, HandshakingXONXOFRadioButton,
                        HandshakingRTSCTSRadioButton, HandshakingRTSCTSXONXOFRadioButton },
                new[] { Handshake.None, Handshake.XOnXOff, Handshake.RequestToSend,
                        Handshake.RequestToSendXOnXOff });
            AppendChoice = new Choice<Settings.Port.AppendType>(
                new[] { AppendNoneRadioButton, AppendCRRadioButton, AppendLFRadioButton, AppendCRLFRadioButton },
                new[] { Settings.Port.AppendType.AppendNothing, Settings.Port.AppendType.AppendCR,
                        Settings.Port.AppendType.AppendLF, Settings.Port.AppendType.AppendCRLF });

            /* Show the saved settings.  A baud rate without a button is a
               custom rate.  */
            if (!BaudRateChoice.Show(Settings.Port.BaudRate))
            {
                BaudRateCustomRadioButton.Checked = true;
                BaudRateCustomRadioButton_Click(this, EventArgs.Empty);
                CustomBRTextBox.Text = Settings.Port.BaudRate.ToString();
            }
            DataBitsChoice.Show(Settings.Port.DataBits);
            ParityChoice.Show(Settings.Port.Parity);
            StopBitsChoice.Show(Settings.Port.StopBits);
            HandshakeChoice.Show(Settings.Port.Handshake);
            AppendChoice.Show(Settings.Port.Append);

            /* Load saved DTR and RTS settings.  */
            TransmitDTRCheckBox.Checked = Settings.Port.Dtr;
            TransmitRTSCheckBox.Checked = Settings.Port.Rts;
        }

        /* While the port list is open, refresh it when ports appear or
           disappear.  */
        private void ComPortMonitorTimer_Tick(object sender, EventArgs e)
        {
            if (!COMPortComboBox.DroppedDown)
                return;

            string[] currentPorts = SerialPort.GetPortNames();

            if (!lastKnownPorts.SequenceEqual(currentPorts))
            {
                COMPortComboBox.DroppedDown = false;
                COMPortComboBox_Click(this, EventArgs.Empty);
                COMPortComboBox.DroppedDown = true;

                lastKnownPorts = currentPorts;
            }
        }

        /* Start watching for port changes when the list opens.  */
        private void COMPortComboBox_DropDown(object sender, EventArgs e)
        {
            lastKnownPorts = SerialPort.GetPortNames();
            comPortMonitorTimer.Start();
        }

        /* Stop watching when the list closes.  */
        private void COMPortComboBox_DropDownClosed(object sender, EventArgs e)
        {
            comPortMonitorTimer.Stop();
        }

        /* Fill the port list with all available ports and their device
           descriptions from WMI, without duplicates, and select the port from
           the settings.  */
        private void COMPortComboBox_Click(object sender, EventArgs e)
        {
            /* Clear existing items.  */
            COMPortComboBox.Items.Clear();

            /* Get unique COM ports.  */
            string[] portList = SerialPort.GetPortNames().Distinct().ToArray();

            /* Load device descriptions from WMI.  */
            Dictionary<string, string> portDescriptions = new Dictionary<string, string>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Caption FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'");

                foreach (ManagementObject obj in searcher.Get())
                {
                    string caption = obj["Caption"]?.ToString();
                    if (string.IsNullOrWhiteSpace(caption))
                        continue;

                    var match = System.Text.RegularExpressions.Regex.Match(caption, @"\((COM\d+)\)");
                    if (match.Success)
                    {
                        string comPort = match.Groups[1].Value;
                        string desc = caption.Replace($"({comPort})", "").Trim();

                        /* Avoid duplicate keys.  */
                        if (!portDescriptions.ContainsKey(comPort))
                            portDescriptions[comPort] = desc;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("COM port description load failed: " + ex.Message);
            }

            /* Populate ComboBox without duplicates.  */
            HashSet<string> addedItems = new HashSet<string>();
            int selectedIndex = -1;

            for (int i = 0; i < portList.Length; i++)
            {
                string item = portList[i];
                if (portDescriptions.TryGetValue(portList[i], out string desc))
                    item += " - " + desc;

                if (addedItems.Add(item)) /* Skip duplicates.  */
                    COMPortComboBox.Items.Add(item);

                /* Track selection of last-used port.  */
                if (portList[i] == Settings.Port.PortName)
                    selectedIndex = COMPortComboBox.Items.IndexOf(item);
            }

            /* Select last-used port if present.  */
            if (selectedIndex >= 0)
                COMPortComboBox.SelectedIndex = selectedIndex;
        }


        /* Hide the custom baud rate box when a standard rate is chosen.  */
        private void BaudRateRadioButton_Click(object sender, EventArgs e)
        {
            CustomBRTextBox.Visible = CustomBRTextBox.Enabled = CustomBRGroupBox.Visible = CustomBRGroupBox.Enabled = false;
            ComPortGroupBox.Size = new System.Drawing.Size(99, 95);
        }

        /* Show the custom baud rate box.  */
        private void BaudRateCustomRadioButton_Click(object sender, EventArgs e)
        {
            CustomBRTextBox.Visible = CustomBRTextBox.Enabled = CustomBRGroupBox.Visible = CustomBRGroupBox.Enabled = true;
            ComPortGroupBox.Size = new System.Drawing.Size(99, 46);
        }

        /* Check the custom baud rate, store all choices in the settings, save
           them and close the dialog with OK.  If no port is selected, the
           previous port is kept.  */
        private void SaveOptions_Click(object sender, EventArgs e)
        {
            /* Validate custom baud rate before changing anything.  */
            int customBaud = 0;
            if (BaudRateCustomRadioButton.Checked &&
                (!int.TryParse(CustomBRTextBox.Text, out customBaud) || customBaud <= 0))
            {
                MessageBox.Show("Please enter a valid custom baud rate (a positive number).",
                                "Invalid Baud Rate",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                CustomBRTextBox.Focus();
                return;
            }

            /* Save the port, keeping the previous one if none is selected.  */
            string portName = COMPortComboBox.Text.Split('-')[0].Trim();
            if (!string.IsNullOrEmpty(portName))
                Settings.Port.PortName = portName;

            /* Save the checked choices.  A group with no button checked gets
               a fixed default.  */
            Settings.Port.BaudRate = BaudRateCustomRadioButton.Checked
                ? customBaud : BaudRateChoice.Selected(9600);
            Settings.Port.DataBits = DataBitsChoice.Selected(8);
            Settings.Port.Append = AppendChoice.Selected(Settings.Port.AppendType.AppendCRLF);
            Settings.Port.Parity = ParityChoice.Selected(Parity.Space);
            Settings.Port.StopBits = StopBitsChoice.Selected(StopBits.Two);
            Settings.Port.Handshake = HandshakeChoice.Selected(Handshake.RequestToSendXOnXOff);

            /* Save DTR/RTS.  */
            Settings.Port.Dtr = TransmitDTRCheckBox.Checked;
            Settings.Port.Rts = TransmitRTSCheckBox.Checked;

            /* Write settings to XML.  */
            Settings.WriteXml();

            /* Close form, reporting that settings were saved.  */
            DialogResult = DialogResult.OK;
        }

        /* Ignore right-clicks on the title bar and borders (the system menu)
           and double-clicks on the title bar (maximize); pass every other
           message BUFFER on.  */
        protected override void WndProc(ref Message buffer)
        {
            if (!Main_Form.IsBlockedNonClientMessage(buffer.Msg))
                base.WndProc(ref buffer);
        }
    }
}
