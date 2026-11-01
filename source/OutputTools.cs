/* OutputTools.cs -- timestamps, highlighting, logging to a file and the
   find bar of the output window.  Part of the Main_Form class.

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace _4Term
{
    public partial class Main_Form
    {
        /*----------------------------------------------------------
         * State
         *---------------------------------------------------------*/

        /* True if the next text starts a new output line.  */
        private bool TextAtLineStart = true;
        /* Where the current output line starts in the text box.  */
        private int LineStart;
        /* Text of the current output line so far, without its timestamp,
           for highlighting.  */
        private readonly StringBuilder CurrentLine = new StringBuilder();

        /* The log file, and whether the next logged text starts a line.  */
        private readonly OutputLog Log = new OutputLog();
        private bool LogAtLineStart = true;

        /* Highlight rules from the settings: text and color, in order.  */
        private string[] HighlightTexts = new string[0];
        private Color[] HighlightColors = new Color[0];
        /* True for a rule that colors only its matches, not the line.  */
        private bool[] HighlightWords = new bool[0];
        /* Where the text of the current line starts, after its
           timestamp.  */
        private int LineBodyStart;

        /* Menu items added in code.  */
        private ToolStripMenuItem FindMenuItem, FindNextMenuItem, FindPreviousMenuItem;
        private ToolStripMenuItem TimestampsMenuItem, HighlightsMenuItem;
        private ToolStripMenuItem LogMenuItem, LogFileMenuItem;

        /* The find bar over the output window.  */
        private Panel FindBar;
        private TextBox FindBox;
        private CheckBox FindCase;
        private Label FindNote;

        /* The highlight editor, shown over the output window; see
           HighlightPane.cs.  */
        private readonly HighlightPane Highlights = new HighlightPane();

        /*----------------------------------------------------------
         * Setting up
         *---------------------------------------------------------*/

        /* Add the menu items for finding, timestamps, highlights and the
           log, and create the find bar and the highlight editor.  */
        private void CreateOutputTools()
        {
            /* Edit menu: Find, Find Next and Find Previous.  */
            FindMenuItem = new ToolStripMenuItem("&Find...") { ShortcutKeys = Keys.Control | Keys.F };
            FindMenuItem.Click += (s, e) => ShowFindBar();
            FindNextMenuItem = new ToolStripMenuItem("Find &Next") { ShortcutKeys = Keys.F3 };
            FindNextMenuItem.Click += (s, e) => FindNext(true);
            FindPreviousMenuItem = new ToolStripMenuItem("Find &Previous") { ShortcutKeys = Keys.Shift | Keys.F3 };
            FindPreviousMenuItem.Click += (s, e) => FindNext(false);
            EditToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripSeparator(), FindMenuItem, FindNextMenuItem, FindPreviousMenuItem
            });

            /* View menu: Timestamps and Highlights.  */
            TimestampsMenuItem = new ToolStripMenuItem("&Timestamps") { ShortcutKeys = Keys.Control | Keys.Shift | Keys.T };
            TimestampsMenuItem.Click += (s, e) =>
            {
                Settings.Output.Timestamps = !Settings.Output.Timestamps;
                TimestampsMenuItem.Checked = Settings.Output.Timestamps;
                Settings.WriteXml();
            };
            HighlightsMenuItem = new ToolStripMenuItem("&Highlights...") { ShortcutKeys = Keys.F7 };
            HighlightsMenuItem.Click += (s, e) => SetHighlightPane(!HighlightPaneVisible);
            int index = ViewToolStripMenuItem.DropDownItems.IndexOf(HexViewToolStripMenuItem);
            ViewToolStripMenuItem.DropDownItems.Insert(index + 1, TimestampsMenuItem);
            ViewToolStripMenuItem.DropDownItems.Insert(index + 2, HighlightsMenuItem);

            /* File menu: Log to File and Log File.  */
            LogMenuItem = new ToolStripMenuItem("&Log to File") { ShortcutKeys = Keys.Control | Keys.L };
            LogMenuItem.Click += (s, e) => SetLogging(!Log.IsOpen, true);
            LogFileMenuItem = new ToolStripMenuItem("Log F&ile...");
            LogFileMenuItem.Click += (s, e) => ChooseLogFile();
            int saveAs = FileToolStripMenuItem.DropDownItems.IndexOf(SaveAsToolStripMenuItem);
            FileToolStripMenuItem.DropDownItems.Insert(saveAs + 1, LogMenuItem);
            FileToolStripMenuItem.DropDownItems.Insert(saveAs + 2, LogFileMenuItem);

            foreach (ToolStripMenuItem item in new[] { FindMenuItem, FindNextMenuItem, FindPreviousMenuItem,
                                                       TimestampsMenuItem, HighlightsMenuItem, LogMenuItem, LogFileMenuItem })
                item.Font = MacroModeToolStripMenuItem.Font;

            CreateFindBar();

            /* The highlight editor lies over the output, hidden.  */
            Highlights.Visible = false;
            Highlights.Bounds = RichTextBox.Bounds;
            Highlights.Anchor = RichTextBox.Anchor;
            Controls.Add(Highlights);
            Highlights.BringToFront();
            Highlights.CloseRequested += () => SetHighlightPane(false);
            Highlights.Saved += () => { LoadHighlights(); RecolorOutput(); };
        }

        /* Apply the output tool settings after loading them: the menu
           checks, the highlight rules and the log.  */
        private void ApplyOutputToolSettings()
        {
            TimestampsMenuItem.Checked = Settings.Output.Timestamps;
            LoadHighlights();
            if (Settings.Log.Enabled != Log.IsOpen ||
                (Log.IsOpen && !string.Equals(Log.Path, Settings.Log.Path, StringComparison.OrdinalIgnoreCase)))
                SetLogging(Settings.Log.Enabled, false);
            LogMenuItem.Checked = Log.IsOpen;
        }

        /*----------------------------------------------------------
         * Adding text: timestamps and highlighting
         *---------------------------------------------------------*/

        /* Append TEXT in COLOR line by line.  If DECORATE, start each line
           with a timestamp when timestamps are on, and color each finished
           line by the highlight rules.  */
        private void AppendLines(string text, Color color, bool decorate)
        {
            if (RichTextBox.TextLength == 0)
            {
                TextAtLineStart = true;
                LineStart = 0;
                CurrentLine.Clear();
            }

            int i = 0;
            while (i < text.Length)
            {
                int end = text.IndexOfAny(LineBreaks, i);
                int bodyLength = (end < 0 ? text.Length : end) - i;
                if (bodyLength > 0)
                {
                    if (TextAtLineStart && decorate && Settings.Output.Timestamps)
                        AppendRaw(Timestamp(), TimestampColor);
                    if (TextAtLineStart)
                        LineBodyStart = RichTextBox.TextLength;
                    string body = text.Substring(i, bodyLength);
                    AppendRaw(body, color);
                    if (CurrentLine.Length < 4096)
                        CurrentLine.Append(body);
                    TextAtLineStart = false;
                }
                if (end < 0)
                    break;

                /* A line ends at CR LF, LF or a lone CR.  */
                int breakLength = text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n' ? 2 : 1;
                AppendRaw(text.Substring(end, breakLength), color);
                if (decorate)
                    HighlightLine(LineStart, RichTextBox.TextLength, LineBodyStart, CurrentLine.ToString());
                LineStart = RichTextBox.TextLength;
                TextAtLineStart = true;
                CurrentLine.Clear();
                i = end + breakLength;
            }
        }

        private static readonly char[] LineBreaks = { '\r', '\n' };

        /* The current time as a line prefix, such as "12:34:56.789 ".  */
        private static string Timestamp() => DateTime.Now.ToString("HH:mm:ss.fff ");

        /* Color of timestamps: the received color, faded toward the
           background.  */
        private Color TimestampColor => Blend(ReceiveColor, BackgroundColor, 0.5);

        /* The output was cut by REMOVED characters at its start; keep the
           line start in step.  */
        private void OutputTrimmed(int removed)
        {
            LineStart = Math.Max(0, LineStart - removed);
            LineBodyStart = Math.Max(0, LineBodyStart - removed);
        }

        /* After hex output, text starts on a new line.  */
        private void OutputLineEnded()
        {
            TextAtLineStart = true;
            LineStart = RichTextBox.TextLength;
            CurrentLine.Clear();
        }

        /* Read the highlight rules from the settings.  */
        private void LoadHighlights()
        {
            var texts = new System.Collections.Generic.List<string>();
            var colors = new System.Collections.Generic.List<Color>();
            var words = new System.Collections.Generic.List<bool>();
            for (int i = 0; i < Settings.Highlight.Count; i++)
            {
                string text = (Settings.Highlight.Text[i] ?? "").Trim();
                if (text.Length == 0)
                    continue;
                texts.Add(text);
                colors.Add(HighlightPane.ParseColor(Settings.Highlight.Color[i]));
                words.Add(HighlightPane.IsWordMode(Settings.Highlight.Mode[i]));
            }
            HighlightTexts = texts.ToArray();
            HighlightColors = colors.ToArray();
            HighlightWords = words.ToArray();
        }

        /* Return the color of the first whole-line highlight rule that LINE
           matches, or Color.Empty if none does.  */
        private Color HighlightFor(string line)
        {
            for (int i = 0; i < HighlightTexts.Length; i++)
                if (!HighlightWords[i] && line.IndexOf(HighlightTexts[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return HighlightColors[i];
            return Color.Empty;
        }

        /* Color the output line from START to END, whose text LINE begins
           at BODYSTART (after its timestamp): the whole line by the first
           whole-line rule that matches, then each match of every word rule
           on top.  */
        private void HighlightLine(int start, int end, int bodyStart, string line)
        {
            if (HighlightTexts.Length == 0 || end <= start)
                return;
            ColorLine(start, end, bodyStart, line);
            RichTextBox.SelectionStart = RichTextBox.TextLength;
            RichTextBox.SelectionLength = 0;
        }

        /* Do the coloring of HighlightLine; the selection is left
           anywhere.  */
        private void ColorLine(int start, int end, int bodyStart, string line)
        {
            Color color = HighlightFor(line);
            if (!color.IsEmpty)
            {
                RichTextBox.Select(start, end - start);
                RichTextBox.SelectionColor = color;
            }

            for (int i = 0; i < HighlightTexts.Length; i++)
            {
                if (!HighlightWords[i])
                    continue;
                string word = HighlightTexts[i];
                int at = line.IndexOf(word, StringComparison.OrdinalIgnoreCase);
                while (at >= 0)
                {
                    RichTextBox.Select(bodyStart + at, word.Length);
                    RichTextBox.SelectionColor = HighlightColors[i];
                    at = line.IndexOf(word, at + word.Length, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        /* Color the lines already in the output by the new highlight rules.
           Lines that no longer match keep their color.  Very long outputs
           are left alone, as this would take too long.  */
        private void RecolorOutput()
        {
            if (HighlightTexts.Length == 0 || RichTextBox.TextLength > 4000000)
                return;

            int selectionStart = RichTextBox.SelectionStart;
            int selectionLength = RichTextBox.SelectionLength;
            SetRedraw(false);
            try
            {
                string text = RichTextBox.Text;
                int start = 0;
                while (start < text.Length)
                {
                    int end = text.IndexOf('\n', start);
                    if (end < 0)
                        end = text.Length;
                    if (end > start)
                        ColorLine(start, end, start, text.Substring(start, end - start));
                    start = end + 1;
                }
            }
            finally
            {
                RestoreSelection(selectionStart, selectionLength);
                SetRedraw(true);
            }
        }

        /*----------------------------------------------------------
         * Logging
         *---------------------------------------------------------*/

        /* Write TEXT to the log, if it is open, with a timestamp at the start
           of each line when timestamps are on.  */
        private void LogText(string text)
        {
            if (!Log.IsOpen || text.Length == 0)
                return;

            string logged = text;
            if (Settings.Output.Timestamps)
            {
                var stamped = new StringBuilder(text.Length + 16);
                foreach (char c in text)
                {
                    if (LogAtLineStart && c != '\r' && c != '\n')
                    {
                        stamped.Append(Timestamp());
                        LogAtLineStart = false;
                    }
                    stamped.Append(c);
                    if (c == '\n' || c == '\r')
                        LogAtLineStart = true;
                }
                logged = stamped.ToString();
            }

            if (!Log.Write(logged))
            {
                Settings.Log.Enabled = false;
                LogMenuItem.Checked = false;
                ErrorMessage($"Logging stopped: could not write to {Settings.Log.Path}\n");
            }
        }

        /* Start or stop logging as ON says.  When starting without a log
           file, ask for one if ASK, else do nothing.  Save the setting.  */
        private void SetLogging(bool on, bool ask)
        {
            if (!on)
            {
                Log.Close();
                Settings.Log.Enabled = false;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Settings.Log.Path) && !(ask && ChooseLogPath()))
                    return;
                try
                {
                    Log.Open(Settings.Log.Path);
                    LogAtLineStart = true;
                    Settings.Log.Enabled = true;
                    InfoMessage($"Logging to {Settings.Log.Path}\n");
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                           ex is ArgumentException || ex is NotSupportedException)
                {
                    Settings.Log.Enabled = false;
                    ErrorMessage($"Cannot log to {Settings.Log.Path}: {ex.Message}\n");
                }
            }
            LogMenuItem.Checked = Log.IsOpen;
            Settings.WriteXml();
        }

        /* Let the user choose the log file; return true if one was chosen.  */
        private bool ChooseLogPath()
        {
            using (var dialog = new SaveFileDialog
            {
                Title = "Log File",
                Filter = "Log files (*.log;*.txt)|*.log;*.txt|All files (*.*)|*.*",
                OverwritePrompt = false,
                FileName = string.IsNullOrWhiteSpace(Settings.Log.Path)
                    ? $"4term-{DateTime.Now:yyyyMMdd}.log"
                    : Path.GetFileName(Settings.Log.Path)
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return false;
                Settings.Log.Path = dialog.FileName;
                return true;
            }
        }

        /* File > Log File: choose a log file, and continue logging there if
           logging is on.  */
        private void ChooseLogFile()
        {
            if (!ChooseLogPath())
                return;
            if (Log.IsOpen)
                SetLogging(true, false);
            else
                Settings.WriteXml();
        }

        /*----------------------------------------------------------
         * Find bar
         *---------------------------------------------------------*/

        /* Create the find bar, hidden, at the top right of the output.  */
        private void CreateFindBar()
        {
            FindBar = new Panel
            {
                Visible = false,
                Size = new Size(360, 30),
                BackColor = SystemColors.Control,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            FindBox = new TextBox { Location = new Point(4, 4), Width = 170 };
            var previous = new Button { Text = "▲", Location = new Point(178, 3), Size = new Size(28, 22), TabStop = false };
            var next = new Button { Text = "▼", Location = new Point(208, 3), Size = new Size(28, 22), TabStop = false };
            FindCase = new CheckBox { Text = "Aa", Location = new Point(240, 5), AutoSize = true, TabStop = false };
            FindNote = new Label { Location = new Point(282, 8), AutoSize = true, ForeColor = Color.Firebrick };
            var close = new Button { Text = "x", Location = new Point(330, 3), Size = new Size(24, 22), TabStop = false };

            var tips = new ToolTip();
            tips.SetToolTip(previous, "Find previous (Shift+Enter, Shift+F3)");
            tips.SetToolTip(next, "Find next (Enter, F3)");
            tips.SetToolTip(FindCase, "Match case");
            tips.SetToolTip(close, "Close (Esc)");

            previous.Click += (s, e) => FindNext(false);
            next.Click += (s, e) => FindNext(true);
            close.Click += (s, e) => HideFindBar();
            FindBox.TextChanged += (s, e) => FindNote.Text = "";
            FindBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    FindNext(!e.Shift);
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    HideFindBar();
                    e.SuppressKeyPress = true;
                }
            };

            FindBar.Controls.AddRange(new Control[] { FindBox, previous, next, FindCase, FindNote, close });
            Controls.Add(FindBar);

            /* The bar is laid out for the designer's font; scale it like the
               rest of the window.  */
            FindBar.Scale(new SizeF(CurrentAutoScaleDimensions.Width / 6f, CurrentAutoScaleDimensions.Height / 13f));
        }

        /* Show the find bar over the top right of the output, with the
           selected text, if any, to find.  */
        private void ShowFindBar()
        {
            if (MacroBufferVisible || HighlightPaneVisible)
                return;
            FindBar.Location = new Point(RichTextBox.Right - FindBar.Width - SystemInformation.VerticalScrollBarWidth - 4,
                                         RichTextBox.Top + 4);
            if (RichTextBox.SelectionLength > 0 && RichTextBox.SelectionLength < 100 &&
                RichTextBox.SelectedText.IndexOf('\n') < 0)
                FindBox.Text = RichTextBox.SelectedText;
            FindBar.Visible = true;
            FindBar.BringToFront();
            FindBox.Focus();
            FindBox.SelectAll();
        }

        /* Hide the find bar and return to the output.  */
        private void HideFindBar()
        {
            FindBar.Visible = false;
            RichTextBox.Focus();
        }

        /* Select the next match after the selection, or the previous one
           before it if not FORWARD, wrapping around at the ends.  */
        private void FindNext(bool forward)
        {
            if (!FindBar.Visible || FindBox.Text.Length == 0)
            {
                ShowFindBar();
                return;
            }

            string text = FindBox.Text;
            RichTextBoxFinds options = FindCase.Checked ? RichTextBoxFinds.MatchCase : RichTextBoxFinds.None;
            int length = RichTextBox.TextLength;
            int found;
            bool wrapped = false;

            if (forward)
            {
                int from = Math.Min(length, RichTextBox.SelectionStart + RichTextBox.SelectionLength);
                found = from < length ? RichTextBox.Find(text, from, length, options) : -1;
                if (found < 0)
                {
                    found = RichTextBox.Find(text, 0, length, options);
                    wrapped = true;
                }
            }
            else
            {
                int to = RichTextBox.SelectionStart;
                options |= RichTextBoxFinds.Reverse;
                found = to > 0 ? RichTextBox.Find(text, 0, to, options) : -1;
                if (found < 0)
                {
                    found = RichTextBox.Find(text, 0, length, options);
                    wrapped = true;
                }
            }

            if (found < 0)
            {
                FindNote.Text = "none";
                return;
            }
            FindNote.Text = wrapped ? "wrap" : "";
            RichTextBox.Select(found, text.Length);
            RichTextBox.ScrollToCaret();
            FindBox.Focus();
        }

        /*----------------------------------------------------------
         * Highlight editor
         *---------------------------------------------------------*/

        /* True while the highlight editor is shown.  */
        private bool HighlightPaneVisible => Highlights.Visible;

        /* Show or hide the highlight editor over the output as SHOW says.
           The macro buffer closes first; hiding asks about unsaved
           changes.  */
        private void SetHighlightPane(bool show)
        {
            if (show == HighlightPaneVisible)
                return;

            if (show)
            {
                if (MacroBufferVisible)
                {
                    SetMacroBuffer(false);
                    if (MacroBufferVisible)
                        return;
                }
                FindBar.Visible = false;
                Highlights.LoadRules();
                Highlights.Bounds = RichTextBox.Bounds;
                Highlights.Visible = true;
                Highlights.BringToFront();
                Highlights.FocusRow(0);
            }
            else
            {
                if (!Highlights.ConfirmLeave())
                    return;
                Highlights.Visible = false;
                RichTextBox.Focus();
            }
            HighlightsMenuItem.Checked = show;
        }
    }
}
