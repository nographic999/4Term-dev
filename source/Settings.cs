/* Settings.cs -- all user settings with their defaults, and loading and saving
   them in config.xml.

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

using System;
using System.Collections.Generic;
using System.IO.Ports;

namespace _4Term
{
    /* Settings shared by the whole program, loaded by ReadXml and saved by
       WriteXml.  */
    public class Settings
    {
        /* Program behavior.  */
        public class Program
        {
            /* True until the first run has finished; the first run shows the
               welcome animation.  */
            public static bool FirstStart = true;
            /* Show the welcome animation on every start.  */
            public static bool ShowWelcomeMessage = false;
            /* Show the button that switches between page groups.  */
            public static bool IsSwapButtonVisible = true;
        }

        /* Main window.  */
        public class Window
        {
            /* Window width in pixels at startup.  */
            public static int Width = 718;
            /* Window height in pixels at startup.  */
            public static int Height = 525;
        }

        /* Output window.  */
        public class Output
        {
            /* Font family.  */
            public static string Font = "Consolas";
            /* Font style: Regular, Bold, Italic, or "Bold, Italic".  */
            public static string FontStyle = "Regular";
            /* Font size in points.  */
            public static float Size = 10;
            /* Strike out the text.  */
            public static bool Strikeout = false;
            /* Underline the text.  */
            public static bool Underline = false;
            /* Wrap long lines.  */
            public static bool WordWrap = true;
            /* Scroll to new output as it arrives.  */
            public static bool Scroll = true;
            /* Show data as a hex dump instead of text.  */
            public static bool HexOutput = false;
            /* Show sent commands in the output.  */
            public static bool LocalEcho = true;
            /* Log size limit in millions of characters (1-512); beyond it the
               oldest text is removed.  */
            public static int MaxLogMB = 16;
            /* Start each output line with the time it began, such as
               "12:34:56.789".  */
            public static bool Timestamps = false;
        }

        /* Logging the output to a file as it arrives.  */
        public class Log
        {
            /* Log while this is on.  */
            public static bool Enabled = false;
            /* The log file; new output is added at its end.  */
            public static string Path = "";
        }

        /* Highlighting: output lines that contain Text[i], ignoring case,
           are shown in Color[i], "#RRGGBB".  Mode[i] is "line" to color the
           whole line, where the first such rule that matches wins, or
           "word" to color only each match of the text, on top.  */
        public class Highlight
        {
            /* Number of rules.  */
            public const int Count = 64;
            /* By default lines with "ERROR" are red.  */
            public static string[] Text = Starting("ERROR");
            public static string[] Color = Starting("#FF5555");
            public static string[] Mode = Starting("line");

            /* Return Count empty strings, the first one FIRST.  */
            private static string[] Starting(string first)
            {
                var items = new string[Count];
                for (int i = 0; i < Count; i++)
                    items[i] = "";
                items[0] = first;
                return items;
            }
        }

        /* Colors, as red, green and blue values from 0 to 255.  */
        public class Colors
        {
            /* Background of the output window.  */
            public static (int R, int G, int B) Back = (30, 30, 30);
            /* Sent data and 4Term's own messages.  */
            public static (int R, int G, int B) Transmit = (194, 30, 86);
            /* Received data and script output.  */
            public static (int R, int G, int B) Receive = (152, 255, 204);
        }      
        
        /* Serial port.  */
        public class Port
        {      
            /* Port name, such as COM3.  */
            public static string PortName = "COM1";
            /* Speed in bits per second.  */
            public static int BaudRate = 9600;
            /* Data bits per character, 5 to 8.  */
            public static int DataBits = 8;
            /* Parity checking.  */
            public static System.IO.Ports.Parity Parity = System.IO.Ports.Parity.None;
            /* Number of stop bits.  */
            public static System.IO.Ports.StopBits StopBits = System.IO.Ports.StopBits.One;
            /* Flow control: none, XON/XOFF, RTS/CTS or both.  */
            public static System.IO.Ports.Handshake Handshake = System.IO.Ports.Handshake.None;
            /* State of the DTR line.  */
            public static bool Dtr = false;
            /* State of the RTS line.  */
            public static bool Rts = false;

            /* Line endings that Send can append.  */
            public enum AppendType
            {
                AppendNothing,
                AppendCR,
                AppendLF,
                AppendCRLF
            }

            /* Line ending appended to sent commands.  */
            public static AppendType Append = AppendType.AppendCR;
        }

        /* Layout of the macros.  A page shows ButtonsPerPage macro buttons;
           PagesPerGroup pages, chosen with the radio buttons, form a group;
           and each group has its own bank of macros.  The swap button cycles
           through the groups.

           ButtonsPerPage and PagesPerGroup must match the buttons on the main
           window.  Groups can be changed freely: config.xml then simply gains
           or loses page and macro entries.  */
        public static class Layout
        {
            /* Macro buttons on the main window.  */
            public const int ButtonsPerPage = 16;
            /* Page radio buttons on the main window.  */
            public const int PagesPerGroup = 8;
            /* Number of page groups, each with its own macro bank.  */
            public const int Groups = 2;

            /* Pages in all groups.  */
            public const int Pages = PagesPerGroup * Groups;
            /* Macros in one group's bank.  */
            public const int MacrosPerGroup = ButtonsPerPage * PagesPerGroup;
            /* Macros in all banks.  */
            public const int Macros = MacrosPerGroup * Groups;
        }

        /* Names of the macro pages.  */
        public class Page
        {
            public static string[] Text = new string[Layout.Pages];
        }

        /* Labels of the macro buttons.  */
        public class MacroText
        {
            public static string[] Text = new string[Layout.Macros];
        }

        /* Commands of the macro buttons.  */
        public class MacroCommand
        {
            public static string[] Text = new string[Layout.Macros];
        }

        /* Repeat intervals of the macros in milliseconds, as text; empty
           for a macro that does not repeat.  */
        public class MacroRepeat
        {
            public static string[] Interval = new string[Layout.Macros];
        }

        /* The macro buffer, where the macros of a page are edited.  */
        public class MacroBuffer
        {
            /* Commands the buffer marks as risky, separated by ";".  A macro
               is risky if its command starts with one of them, ignoring
               case.  */
            public static string RiskyCommands = "AT+CFUN=1,1;AT+QPOWD;AT&F";
        }

        /* Every setting stored in config.xml, in file order.  ReadXml and
           WriteXml both work from this one list, so a setting is added in
           one place.  */
        private static readonly List<Entry> Entries = BuildEntries();

        /* One setting in config.xml: how to read it into the program and how
           to write it back.  */
        private sealed class Entry
        {
            public readonly Action Read;
            public readonly Action Write;

            public Entry(Action read, Action write)
            {
                Read = read;
                Write = write;
            }
        }

        /* Return the list of all settings.  A value that is missing or
           invalid in the file keeps its current value, so older or
           hand-edited files load instead of failing.  */
        private static List<Entry> BuildEntries()
        {
            var entries = new List<Entry>
            {
                /* Program section.  */
                BoolSetting("Program/FirstStart", () => Program.FirstStart, v => Program.FirstStart = v),
                BoolSetting("Program/ShowWelcomeMessage", () => Program.ShowWelcomeMessage, v => Program.ShowWelcomeMessage = v),
                BoolSetting("Program/IsSwapButtonVisible", () => Program.IsSwapButtonVisible, v => Program.IsSwapButtonVisible = v),

                /* Form section.  */
                IntSetting("Form/Width", 100, 10000, () => Window.Width, v => Window.Width = v),
                IntSetting("Form/Height", 100, 10000, () => Window.Height, v => Window.Height = v),

                /* RichTextBox section.  */
                NameSetting("RichTextBox/Font", () => Output.Font, v => Output.Font = v),
                NameSetting("RichTextBox/FontStyle", () => Output.FontStyle, v => Output.FontStyle = v),
                FloatSetting("RichTextBox/Size", 1, 100, () => Output.Size, v => Output.Size = v),
                BoolSetting("RichTextBox/Strikeout", () => Output.Strikeout, v => Output.Strikeout = v),
                BoolSetting("RichTextBox/Underline", () => Output.Underline, v => Output.Underline = v),
                BoolSetting("RichTextBox/WordWrap", () => Output.WordWrap, v => Output.WordWrap = v),
                BoolSetting("RichTextBox/Scroll", () => Output.Scroll, v => Output.Scroll = v),
                BoolSetting("RichTextBox/HexOutput", () => Output.HexOutput, v => Output.HexOutput = v),
                BoolSetting("RichTextBox/LocalEcho", () => Output.LocalEcho, v => Output.LocalEcho = v),
                IntSetting("RichTextBox/MaxLogMB", 1, 512, () => Output.MaxLogMB, v => Output.MaxLogMB = v),
                BoolSetting("RichTextBox/Timestamps", () => Output.Timestamps, v => Output.Timestamps = v),

                /* Log section.  */
                BoolSetting("Log/Enabled", () => Log.Enabled, v => Log.Enabled = v),
                TextSetting("Log/Path", () => Log.Path, v => Log.Path = v),

                /* Color section.  */
                IntSetting("Back/R", 0, 255, () => Colors.Back.R, v => Colors.Back.R = v),
                IntSetting("Back/G", 0, 255, () => Colors.Back.G, v => Colors.Back.G = v),
                IntSetting("Back/B", 0, 255, () => Colors.Back.B, v => Colors.Back.B = v),
                IntSetting("Transmit/R", 0, 255, () => Colors.Transmit.R, v => Colors.Transmit.R = v),
                IntSetting("Transmit/G", 0, 255, () => Colors.Transmit.G, v => Colors.Transmit.G = v),
                IntSetting("Transmit/B", 0, 255, () => Colors.Transmit.B, v => Colors.Transmit.B = v),
                IntSetting("Receive/R", 0, 255, () => Colors.Receive.R, v => Colors.Receive.R = v),
                IntSetting("Receive/G", 0, 255, () => Colors.Receive.G, v => Colors.Receive.G = v),
                IntSetting("Receive/B", 0, 255, () => Colors.Receive.B, v => Colors.Receive.B = v),

                /* Port section.  */
                NameSetting("Port/PortName", () => Port.PortName, v => Port.PortName = v),
                IntSetting("Port/BaudRate", 1, int.MaxValue, () => Port.BaudRate, v => Port.BaudRate = v),
                IntSetting("Port/DataBits", 5, 8, () => Port.DataBits, v => Port.DataBits = v),
                EnumSetting("Port/Parity", () => Port.Parity, v => Port.Parity = v),
                EnumSetting("Port/StopBits", () => Port.StopBits, v => Port.StopBits = v),
                EnumSetting("Port/Handshake", () => Port.Handshake, v => Port.Handshake = v),
                EnumSetting("Port/AppendToSend", () => Port.Append, v => Port.Append = v),
                BoolSetting("Port/Dtr", () => Port.Dtr, v => Port.Dtr = v),
                BoolSetting("Port/Rts", () => Port.Rts, v => Port.Rts = v),
            };

            /* MacroBuffer section.  */
            entries.Add(TextSetting("MacroBuffer/RiskyCommands", () => MacroBuffer.RiskyCommands, v => MacroBuffer.RiskyCommands = v));

            /* Page, MacroText, MacroCommand and MacroRepeat sections.  */
            AddItems(entries, "Page", () => Page.Text);
            AddItems(entries, "MacroText", () => MacroText.Text);
            AddItems(entries, "MacroCommand", () => MacroCommand.Text);
            AddItems(entries, "MacroRepeat", () => MacroRepeat.Interval);

            /* HighlightText, HighlightColor and HighlightMode sections.  */
            AddItems(entries, "HighlightText", () => Highlight.Text);
            AddItems(entries, "HighlightColor", () => Highlight.Color);
            AddItems(entries, "HighlightMode", () => Highlight.Mode);

            return entries;
        }

        /* Return a true/false setting at PATH.  GET and SET access its
           value.  */
        private static Entry BoolSetting(string path, Func<bool> get, Action<bool> set) =>
            new Entry(() => set(Xml.ReadBoolParm(path, get())),
                      () => Xml.WriteBoolParm(path, get()));

        /* Return an integer setting at PATH that must lie in MIN to MAX.  GET
           and SET access its value.  */
        private static Entry IntSetting(string path, int min, int max, Func<int> get, Action<int> set) =>
            new Entry(() => set(ReadIntInRange(path, get(), min, max)),
                      () => Xml.WriteIntParm(path, get()));

        /* Return a number setting at PATH that must lie in MIN to MAX.  GET
           and SET access its value.  */
        private static Entry FloatSetting(string path, float min, float max, Func<float> get, Action<float> set) =>
            new Entry(() =>
                      {
                          float value = Xml.ReadFloatParm(path, get());
                          set(value >= min && value <= max ? value : get());
                      },
                      () => Xml.WriteFloatParm(path, get()));

        /* Return a text setting at PATH that must not be blank, such as a
           font or port name; it is read trimmed.  GET and SET access its
           value.  */
        private static Entry NameSetting(string path, Func<string> get, Action<string> set) =>
            new Entry(() => set(ReadNonEmptyString(path, get())),
                      () => Xml.WriteStringParm(path, get()));

        /* Return a text setting at PATH that may be empty.  GET and SET access
           its value.  */
        private static Entry TextSetting(string path, Func<string> get, Action<string> set) =>
            new Entry(() => set(Xml.ReadStringParm(path, get())),
                      () => Xml.WriteStringParm(path, get()));

        /* Return a setting at PATH holding a value of enum T, stored by name.
           GET and SET access its value.  */
        private static Entry EnumSetting<T>(string path, Func<T> get, Action<T> set) where T : struct =>
            new Entry(() => set(ReadEnum(path, get())),
                      () => Xml.WriteStringParm(path, get().ToString()));

        /* Add to ENTRIES one text setting per element of the array that ITEMS
           returns, at SECTION/Item0, SECTION/Item1 and so on.  */
        private static void AddItems(List<Entry> entries, string section, Func<string[]> items)
        {
            for (int i = 0; i < items().Length; i++)
            {
                int index = i;
                entries.Add(TextSetting($"{section}/Item{index}",
                                        () => items()[index],
                                        v => items()[index] = v));
            }
        }

        /* Load all settings from config.xml.  If the file is missing, offer to
           create it; if it is damaged, offer to back it up and start a new
           one.  Exit the program if the user declines or the file cannot be
           created.  A missing or invalid value keeps its current value.  */
        public static void ReadXml()
        {
            /* Load the file, or create it.  */
            if (!Xml.LoadFile())
            {
                bool ask = true;

                /* File exists but is damaged: offer to back it up and start
                   fresh.  */
                if (Xml.LoadError != null)
                {
                    System.Windows.Forms.DialogResult result = System.Windows.Forms.MessageBox.Show(
                        $"The configuration file is damaged and cannot be read:\n\n{Xml.LoadError}\n\n" +
                        "Back it up and create a new configuration file?",
                        "Damaged Configuration",
                        System.Windows.Forms.MessageBoxButtons.YesNo,
                        System.Windows.Forms.MessageBoxIcon.Warning);

                    if (result != System.Windows.Forms.DialogResult.Yes)
                        Environment.Exit(1);

                    try
                    {
                        System.IO.File.Move(Xml.FileName,
                            Xml.FileName + ".damaged-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                    }
                    catch (Exception ex)
                    {
                        System.Windows.Forms.MessageBox.Show($"Could not back up the configuration file:\n\n{ex.Message}",
                            "Damaged Configuration",
                            System.Windows.Forms.MessageBoxButtons.OK,
                            System.Windows.Forms.MessageBoxIcon.Error);
                        Environment.Exit(1);
                    }

                    ask = false;
                }

                if (!Xml.CreateNew(ask))
                {
                    Environment.Exit(1);
                }
                WriteXml();
                if (!Xml.LoadFile())
                    Environment.Exit(1);
            }

            foreach (Entry entry in Entries)
                entry.Read();
        }

        /* Return the integer at XPATH, or FALLBACK if it is missing, invalid
           or outside MIN to MAX.  */
        private static int ReadIntInRange(string xpath, int fallback, int min, int max)
        {
            int value = Xml.ReadIntParm(xpath, fallback);
            return value >= min && value <= max ? value : fallback;
        }

        /* Return the trimmed text at XPATH, or FALLBACK if it is missing or
           blank.  */
        private static string ReadNonEmptyString(string xpath, string fallback)
        {
            string value = Xml.ReadStringParm(xpath);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        /* Return the value of enum T named at XPATH, ignoring case, or
           FALLBACK if it is missing or not a defined value of T.  */
        private static T ReadEnum<T>(string xpath, T fallback) where T : struct
        {
            string value = Xml.ReadStringParm(xpath);
            if (Enum.TryParse(value, true, out T parsed) && Enum.IsDefined(typeof(T), parsed))
                return parsed;
            return fallback;
        }

        /* Save all settings to config.xml.  Do nothing if the file cannot be
           loaded.  */
        public static void WriteXml()
        {
            /* Load the current file; its values are updated in place.  */
            if (!Xml.LoadFile())
                return;

            foreach (Entry entry in Entries)
                entry.Write();

            Xml.CloseFile();
        }
    }
}
