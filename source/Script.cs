/* Script.cs -- the script interpreter: runs the commands in a script file,
   one per line.

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace _4Term
{
    /* What a running script can do with the program.  Main_Form provides
       it, so the interpreter itself knows nothing about the window.  */
    internal interface IScriptHost
    {
        /* Clear the output window.  */
        void ClearOutput();

        /* Show TEXT in the output window as script output.  */
        void Show(string text);

        /* The lines of the output window.  */
        string[] OutputLines { get; }

        /* True while the serial port is open.  */
        bool PortOpen { get; }

        /* Send COMMAND with the configured line ending.  Return the line
           ending, or null if it could not be sent.  */
        string Send(string command);

        /* Add COMMAND to the history of the command box.  */
        void Remember(string command);
    }

    /* A script run.  The commands, one per line:

       - S: clear the output.
       - E text: show TEXT.
       - F <word><message>: show MESSAGE, or "Found" if it is omitted, when
         WORD appears in the output.
       - C command: send COMMAND.
       - D ms: wait MS milliseconds.
       - R<n> [D<ms>] command: send COMMAND N times (1 to 99), waiting MS
         milliseconds before each.

       Errors are shown in the output and the script goes on with the next
       line, except that a failed send stops it.  */
    internal sealed class Script
    {
        /* The program the script runs in.  */
        private readonly IScriptHost Host;

        /* Optional delay prefix "D<ms> " of a repeated command.  A command
           that merely starts with D, such as "DIR", does not match.  */
        private static readonly Regex DelayPrefix = new Regex(@"^D(\d+)\s+(.+)$");

        /* Shown when a command cannot be sent because the port is closed.  */
        private const string PortClosed =
            "The serial port is not open. Cannot send command from script.\n";

        /* Create a script run in HOST.  */
        public Script(IScriptHost host)
        {
            Host = host;
        }

        /* Run the script in the file FILEPATH.  Empty lines are skipped.  */
        public async Task RunAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Host.Show($"Script file not found: {filePath}\n");
                return;
            }

            try
            {
                using (var reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.Length == 0)
                            continue;

                        if (!await RunLineAsync(trimmed))
                            return;
                    }
                }
            }
            catch (Exception ex)
            {
                Host.Show($"Error reading script file: {ex.Message}\n");
            }
        }

        /* Run the script line LINE, which is trimmed and not empty.  Its
           first character is the command letter.  Return false if the
           script must stop.  */
        private async Task<bool> RunLineAsync(string line)
        {
            switch (line[0])
            {
                case 'S':
                    Host.ClearOutput();
                    return true;

                case 'E':
                    Echo(line);
                    return true;

                case 'F':
                    Find(line);
                    return true;

                case 'C':
                    return SendCommand(line);

                case 'D':
                    await DelayAsync(line);
                    return true;

                case 'R':
                    return await RepeatAsync(line);

                default:
                    Host.Show($"Unknown script code '{line[0]}' in line: {line}\n");
                    return true;
            }
        }

        /* Return true if LINE has an argument: a space after the command
           letter and something after the space.  */
        private static bool HasArgument(string line) =>
            line.Length > 2 && line[1] == ' ';

        /* "E text": show TEXT.  */
        private void Echo(string line)
        {
            if (HasArgument(line))
                Host.Show(line.Substring(2) + "\n");
            else
                Host.Show("Invalid echo format (must be 'E <message>')\n");
        }

        /* "F <word><message>": show MESSAGE, or "Found", if WORD appears in
           the output, and say so if it does not.  */
        private void Find(string line)
        {
            const string BadFormat = "Invalid find format (must be 'F <word><message>')\n";

            if (!HasArgument(line))
            {
                Host.Show(BadFormat);
                return;
            }

            string content = line.Substring(2).Trim();
            int firstLt = content.IndexOf('<');
            int firstGt = content.IndexOf('>');
            if (firstLt < 0 || firstGt <= firstLt)
            {
                Host.Show(BadFormat);
                return;
            }

            string word = content.Substring(firstLt + 1, firstGt - firstLt - 1);

            /* The message is optional.  */
            string message = "Found";
            int secondLt = content.IndexOf('<', firstGt + 1);
            int secondGt = content.IndexOf('>', firstGt + 1);
            if (secondLt >= 0 && secondGt > secondLt)
                message = content.Substring(secondLt + 1, secondGt - secondLt - 1);

            bool found = false;
            foreach (string outputLine in Host.OutputLines)
            {
                if (outputLine.Contains(word))
                {
                    found = true;
                    break;
                }
            }

            Host.Show(found ? message + "\n" : $"'{word}' not found.\n");
        }

        /* "C command": send COMMAND.  Return false if sending failed.  */
        private bool SendCommand(string line)
        {
            if (!HasArgument(line))
            {
                Host.Show($"Invalid command format (must be 'C <command>'): {line}\n");
                return true;
            }

            if (!Host.PortOpen)
            {
                Host.Show(PortClosed);
                return true;
            }

            return SendOnce(line.Substring(2));
        }

        /* "D ms": wait MS milliseconds.  */
        private async Task DelayAsync(string line)
        {
            if (!HasArgument(line))
            {
                Host.Show($"Invalid delay format (must be 'D <ms>'): {line}\n");
                return;
            }

            /* Task.Delay waits forever for -1 and throws for other
               negative numbers, so they are refused here.  */
            if (!int.TryParse(line.Substring(2), out int delayMs))
                Host.Show($"Invalid delay in script: {line}\n");
            else if (delayMs < 0)
                Host.Show($"Delay must be 0 ms or more: {line}\n");
            else
                await Task.Delay(delayMs);
        }

        /* "R<n> [D<ms>] command": send COMMAND N times, waiting MS
           milliseconds before each, without local echo.  Stop repeating if
           the port closes.  Return false if sending failed.  */
        private async Task<bool> RepeatAsync(string line)
        {
            int space = line.IndexOf(' ');
            if (space <= 1)
            {
                Host.Show($"Invalid repeat format (must be 'R<num> <command>'): {line}\n");
                return true;
            }

            if (!int.TryParse(line.Substring(1, space - 1), out int count))
            {
                Host.Show($"Invalid repeat number in script: {line}\n");
                return true;
            }

            if (count < 1 || count > 99)
            {
                Host.Show($"Repeat count must be between 1 and 99: {line}\n");
                return true;
            }

            string command = line.Substring(space + 1).Trim();
            int delayMs = 0;
            Match delay = DelayPrefix.Match(command);
            if (delay.Success)
            {
                if (!int.TryParse(delay.Groups[1].Value, out delayMs))
                {
                    Host.Show($"Invalid delay in repeat command: {line}\n");
                    return true;
                }
                command = delay.Groups[2].Value;
            }

            for (int i = 0; i < count; i++)
            {
                if (delayMs > 0)
                    await Task.Delay(delayMs);

                if (!Host.PortOpen || string.IsNullOrEmpty(command))
                {
                    Host.Show(PortClosed);
                    break;
                }

                if (!SendOnce(command))
                    return false;
            }

            return true;
        }

        /* Send COMMAND and add it to the history.  Return false if it could
           not be sent, after saying that the script stops.  */
        private bool SendOnce(string command)
        {
            if (Host.Send(command) == null)
            {
                Host.Show("Send failed, script stopped.\n");
                return false;
            }

            Host.Remember(command);
            return true;
        }
    }
}
