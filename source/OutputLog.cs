/* OutputLog.cs -- writes the output to a log file as it arrives.

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

using System;
using System.IO;
using System.Text;

namespace _4Term
{
    /* The log file.  While it is open, everything added to the output, as
       text, is added to the end of the file and flushed at once, so the log
       is complete even if 4Term or the PC stops.  */
    internal sealed class OutputLog : IDisposable
    {
        private StreamWriter Writer;

        /* The file being written, or null while closed.  */
        public string Path { get; private set; }

        /* True while the log is open.  */
        public bool IsOpen => Writer != null;

        /* Open the file at PATH, adding to its end, and write a line saying
           when logging started.  Throw IOException or
           UnauthorizedAccessException if it cannot be opened.  */
        public void Open(string path)
        {
            Close();
            Writer = new StreamWriter(path, true, new UTF8Encoding(false)) { AutoFlush = true };
            Path = path;
            Writer.Write($"\r\n--- 4Term log started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---\r\n");
        }

        /* Write TEXT.  Return false, closing the log, if writing failed,
           for example because the disk is full or was removed.  */
        public bool Write(string text)
        {
            if (Writer == null)
                return true;
            try
            {
                Writer.Write(text);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
            {
                Close();
                return false;
            }
        }

        /* Write a line saying when logging stopped, and close the file.  */
        public void Close()
        {
            if (Writer == null)
                return;
            try
            {
                Writer.Write($"\r\n--- 4Term log stopped {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---\r\n");
                Writer.Dispose();
            }
            catch (IOException)
            {
                /* The file is gone; there is nothing more to do.  */
            }
            Writer = null;
            Path = null;
        }

        public void Dispose() => Close();
    }
}
