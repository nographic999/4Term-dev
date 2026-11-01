/* MacroBuffer.cs -- the macro buffer: edits the macros of one page at a
   time, below the output window, while data keeps arriving.

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace _4Term
{
    /* How a macro looks in the buffer and on its button.  */
    internal enum MacroState
    {
        Saved,
        Changed,
        Problem
    }

    /* The macro buffer.  It shows the ButtonsPerPage macros of one page as
       editable lines: number, label and command, with tags for changed
       lines, problems, scripts and risky commands, and below them the last
       lines of the output, which the buffer covers.  Edits stay in
       the buffer until Save; the main window asks before they are lost.

       Keys: Enter and arrow keys move between fields, Ctrl+Enter sends the
       current line, Alt+Up and Alt+Down move a macro, Ctrl+S saves and
       Ctrl+Shift+C copies the page as text.  Pasting several "label |
       command" lines fills the lines from the cursor down.  Ctrl+Z right
       after a move or such a paste undoes it.  Esc gives a changed field
       back its text; in an unchanged field it closes the buffer.  */
    internal sealed class MacroBufferPane : UserControl
    {
        /* Asks for page PAGE, an absolute page number; the main window
           decides and calls ShowPage.  */
        public event Action<int> PageRequested;
        /* Asks to send COMMAND now, without saving.  */
        public event Action<string> SendRequested;
        /* Asks to close the buffer.  */
        public event Action CloseRequested;
        /* Raised after any change to what the buffer shows, for the live
           preview on the macro buttons.  */
        public event Action Edited;
        /* Raised after the buffer was saved to the settings.  */
        public event Action Saved;
        /* Raised while saving, before Saved, with where the macros of the
           page went: the new slot for each old one, for those still on the
           page.  */
        public event Action<Dictionary<int, int>> Moved;

        /* Asks QUESTION about unsaved changes; Yes saves, No discards,
           Cancel stays.  Replaceable for tests.  */
        internal Func<string, DialogResult> AskSave = question =>
            MessageBox.Show(question, "Macro Buffer", MessageBoxButtons.YesNoCancel,
                            MessageBoxIcon.Question);

        /* The page shown, an absolute page number.  */
        public int Page { get; private set; }

        /* Colors of the buffer.  */
        private static readonly Color Back = Color.FromArgb(0x16, 0x18, 0x1D);
        private static readonly Color HeaderBack = Color.FromArgb(0x1C, 0x20, 0x28);
        private static readonly Color CurrentBack = Color.FromArgb(0x22, 0x28, 0x33);
        private static readonly Color ChangedBack = Color.FromArgb(0x2A, 0x24, 0x1B);
        private static readonly Color TextColor = Color.FromArgb(0xE5, 0xE7, 0xEB);
        private static readonly Color Dim = Color.FromArgb(0x8A, 0x93, 0xA3);
        private static readonly Color Faint = Color.FromArgb(0x4B, 0x55, 0x63);
        private static readonly Color Amber = Color.FromArgb(0xFC, 0xD3, 0x4D);
        private static readonly Color Red = Color.FromArgb(0xFC, 0xA5, 0xA5);
        private static readonly Color Violet = Color.FromArgb(0xC4, 0xB5, 0xFD);
        private static readonly Color Sky = Color.FromArgb(0x7D, 0xD3, 0xFC);
        private static readonly Color Orange = Color.FromArgb(0xFD, 0xBA, 0x74);
        private static readonly Color ChipBack = Color.FromArgb(0x20, 0x25, 0x2E);
        private static readonly Color ChipBorder = Color.FromArgb(0x34, 0x3B, 0x48);
        private static readonly Color StatusBack = Color.FromArgb(0x2A, 0x2F, 0x3A);
        private static readonly Color StatusText = Color.FromArgb(0xD8, 0xDE, 0xE9);

        /* Heights of the fixed parts, in pixels.  */
        /* Screen pixels per design pixel: 1 at 96 dpi, 1.5 at 144 dpi.
           The buffer's fixed sizes are given at 96 dpi and scaled with
           S.  */
        private static readonly float DpiScale = ReadDpiScale();

        /* Return the screen's dpi divided by 96, or 1 if it is unknown.  */
        private static float ReadDpiScale()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                    return g.DpiX > 0 ? g.DpiX / 96f : 1f;
            }
            catch (Exception)
            {
                return 1f;
            }
        }

        /* Return PIXELS, given at 96 dpi, for the screen's dpi.  */
        private static int S(int pixels) => (int)Math.Round(pixels * DpiScale);

        private static readonly int HeaderHeight = S(34);
        private static readonly int NameHeight = S(28);
        private static readonly int StatusHeight = S(22);

        /* One macro line.  */
        private sealed class Row
        {
            public int Index;
            public Panel Panel;
            public Label Number;
            public TextBox Label;
            public Label Bar;
            /* Divider between the repeat interval and the command.  */
            public Label Bar2;
            public TextBox Command;
            /* Repeat interval in milliseconds, empty for none, and its
               icon.  */
            public Label RepeatIcon;
            public TextBox Repeat;
            public Label Tag;
            /* The saved label, command and repeat interval.  */
            public string SavedLabel = "", SavedCommand = "", SavedRepeat = "";
            /* Slot the line's macro was loaded from, or -1 if it was
               pasted over; it follows the macro when lines move.  */
            public int Origin = -1;

            /* The fields of the line, left to right.  */
            public TextBox[] Fields => new[] { Label, Command, Repeat };
        }

        /* Width of the repeat field and its icon, in pixels.  */
        private static readonly int RepeatWidth = S(60), RepeatIconWidth = S(18);

        private readonly Row[] Rows = new Row[Settings.Layout.ButtonsPerPage];
        private readonly Button[] Chips = new Button[Settings.Layout.PagesPerGroup];
        private readonly Panel Header, NameRow, RowsPanel;
        private readonly FlowLayoutPanel ChipFlow, ActionFlow;
        private readonly Label Title, NameLabel, Hint, Status;
        private readonly TextBox PageName;
        private readonly Button GroupButton, RevertButton, SaveButton;
        private readonly ToolTip ChipTips = new ToolTip();
        private readonly Font UiFont = new Font("Segoe UI", 9f);
        private readonly Font UiBold = new Font("Segoe UI", 9f, FontStyle.Bold);
        /* Consolas, by name, until SetEditorFont: a font made from
           FontFamily.GenericMonospace breaks on Windows once that temporary
           family is collected.  */
        private Font EditorFont = new Font("Consolas", 10f);
        private int RowHeight = 22;
        /* Saved name of the page shown.  */
        private string SavedPageName = "";
        /* Line with the cursor, from 0.  */
        private int CurrentRow;
        /* True while lines are being filled, so no change is reported.  */
        private bool Loading;
        /* One-time note in the status line, such as "Saved".  */
        private string Note = "";

        /* Create the buffer, showing page 0 until ShowPage is called.  */
        public MacroBufferPane()
        {
            BackColor = Back;
            ForeColor = TextColor;
            Font = UiFont;

            /* Header: title, page chips, group switch, Revert and Save.  */
            Header = new Panel { Dock = DockStyle.Top, Height = HeaderHeight, BackColor = HeaderBack, Padding = new Padding(S(8), S(4), S(8), S(4)) };
            ActionFlow = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = HeaderBack };
            ChipFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = HeaderBack };
            Title = new Label { Text = "MACROS", AutoSize = true, ForeColor = Dim, Font = UiBold, Margin = new Padding(0, S(6), S(8), 0) };
            ChipFlow.Controls.Add(Title);
            GroupButton = MakeButton("", (s, e) =>
                RequestPage((Page + Settings.Layout.PagesPerGroup) % Settings.Layout.Pages));
            GroupButton.Margin = new Padding(0, 0, S(10), 0);
            ChipFlow.Controls.Add(GroupButton);
            for (int i = 0; i < Chips.Length; i++)
            {
                int chip = i;
                Chips[i] = MakeButton("", (s, e) => RequestPage(GroupFirstPage + chip));
                ChipFlow.Controls.Add(Chips[i]);
            }
            RevertButton = MakeButton("Revert", (s, e) => Revert());
            SaveButton = MakeButton("Save", (s, e) => Save());
            ActionFlow.Controls.Add(RevertButton);
            ActionFlow.Controls.Add(SaveButton);
            Header.Controls.Add(ChipFlow);
            Header.Controls.Add(ActionFlow);

            /* Page name row.  */
            NameRow = new Panel { Dock = DockStyle.Top, Height = NameHeight, BackColor = Back };
            NameLabel = new Label { Text = "Page name", AutoSize = true, ForeColor = Dim, Location = new Point(S(52), S(6)) };
            PageName = new TextBox { BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(0x0F, 0x11, 0x15), ForeColor = TextColor, Width = S(170), Location = new Point(S(130), S(3)) };
            PageName.TextChanged += (s, e) => Changed();
            PageName.Enter += (s, e) => { SetCurrentRow(-1); FieldStart = PageName.Text; };
            Hint = new Label { AutoSize = true, ForeColor = Dim, Location = new Point(S(312), S(6)) };
            NameRow.Controls.Add(NameLabel);
            NameRow.Controls.Add(PageName);
            NameRow.Controls.Add(Hint);

            /* The macro lines.  */
            RowsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Back, AutoScroll = true };
            for (int i = 0; i < Rows.Length; i++)
                Rows[i] = MakeRow(i);
            RowsPanel.Resize += (s, e) => LayoutRows();

            /* Status line.  */
            Status = new Label { Dock = DockStyle.Bottom, Height = StatusHeight, BackColor = StatusBack, ForeColor = StatusText, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(S(8), 0, 0, 0) };

            /* The last lines of the output, which the buffer covers:
               received data, sent commands and 4Term's messages.  */
            Received = new Label { Dock = DockStyle.Bottom, BackColor = Color.FromArgb(0x10, 0x12, 0x16), ForeColor = Dim, Padding = new Padding(S(8), S(3), S(8), S(3)), Text = "Nothing new in the output since the buffer opened." };

            /* Docked controls are laid out in reverse order of adding.  */
            Controls.Add(RowsPanel);
            Controls.Add(Received);
            Controls.Add(Status);
            Controls.Add(NameRow);
            Controls.Add(Header);


            SetEditorFont(EditorFont);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ChipTips.Dispose();
                UiFont.Dispose();
                UiBold.Dispose();
                EditorFont.Dispose();
            }
            base.Dispose(disposing);
        }

        /*----------------------------------------------------------
         * Building
         *---------------------------------------------------------*/

        /* Return a flat header button with TEXT that runs CLICK.  */
        private Button MakeButton(string text, EventHandler click)
        {
            var button = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = ChipBack,
                ForeColor = TextColor,
                Margin = new Padding(0, 0, S(4), 0),
                Height = S(26),
                TabStop = false
            };
            button.FlatAppearance.BorderColor = ChipBorder;
            button.Click += click;
            FitText(button, text);
            return button;
        }

        /* Set the text of header button BUTTON to TEXT and its width to fit
           it.  */
        private static void FitText(Button button, string text)
        {
            button.Text = text;
            button.Width = Math.Max(S(28), TextRenderer.MeasureText(text, button.Font).Width + S(14));
        }

        /* Return line INDEX with its controls, added to RowsPanel.  */
        private Row MakeRow(int index)
        {
            var row = new Row { Index = index };
            row.Panel = new Panel { BackColor = Back };
            row.Number = new Label { Text = (index + 1).ToString("00"), ForeColor = Dim, TextAlign = ContentAlignment.MiddleRight };
            row.Label = new TextBox { BorderStyle = BorderStyle.None, BackColor = Back, ForeColor = TextColor, TabIndex = index * 3 };
            row.Bar = new Label { Text = "│", ForeColor = Faint, TextAlign = ContentAlignment.MiddleCenter };
            row.Bar2 = new Label { Text = "│", ForeColor = Faint, TextAlign = ContentAlignment.MiddleCenter };
            row.Command = new TextBox { BorderStyle = BorderStyle.None, BackColor = Back, ForeColor = TextColor, TabIndex = index * 3 + 1 };
            row.RepeatIcon = new Label { Text = "~", ForeColor = Faint, TextAlign = ContentAlignment.MiddleCenter };
            row.Repeat = new TextBox { BorderStyle = BorderStyle.None, BackColor = Back, ForeColor = Sky, TabIndex = index * 3 + 2, MaxLength = 7 };
            ChipTips.SetToolTip(row.Repeat, "Repeat every so many ms when the button is clicked; empty for no repeat");
            row.Tag = new Label { ForeColor = Amber, TextAlign = ContentAlignment.MiddleRight, Font = UiFont };

            row.Label.TextChanged += (s, e) => Changed();
            row.Command.TextChanged += (s, e) => Changed();
            row.Repeat.TextChanged += (s, e) => Changed();
            row.Repeat.Enter += (s, e) => { SetCurrentRow(index); FieldStart = row.Repeat.Text; };
            row.Label.Enter += (s, e) => { SetCurrentRow(index); FieldStart = row.Label.Text; };
            row.Command.Enter += (s, e) => { SetCurrentRow(index); FieldStart = row.Command.Text; };

            row.Panel.Controls.AddRange(new Control[] { row.Number, row.Label, row.Bar, row.Command, row.RepeatIcon, row.Repeat, row.Bar2, row.Tag });
            RowsPanel.Controls.Add(row.Panel);
            return row;
        }

        /* Use FONT for the macro lines and size the lines to it.  */
        public void SetEditorFont(Font font)
        {
            float size = Math.Max(8f, Math.Min(11f, font.SizeInPoints));
            EditorFont = new Font(font.FontFamily.Name, size, FontStyle.Regular, GraphicsUnit.Point);

            RowHeight = Math.Max(S(20), EditorFont.Height + S(6));
            foreach (Row row in Rows)
            {
                row.Label.Font = EditorFont;
                row.Command.Font = EditorFont;
                row.Repeat.Font = EditorFont;
                row.Number.Font = EditorFont;
                row.Bar.Font = EditorFont;
                row.Bar2.Font = EditorFont;
            }
            PageName.Font = EditorFont;
            Received.Font = EditorFont;
            Received.Height = (EditorFont.Height + 2) * ReceivedLines + 12;
            LayoutRows();

            /* The old font is not disposed: a control that still held it
               would fail with "Parameter is not valid" on Windows, and the
               font changes only when the settings do.  */
        }

        /* Text of the focused field when it got the focus, for Esc.  */
        private string FieldStart = "";

        /* Lines shown in the strip of output lines.  */
        private const int ReceivedLines = 3;
        /* The strip of output lines.  */
        private readonly Label Received;
        /* Lines added to the output since the buffer was shown, and the
           last few.  */
        private int NewLines;
        private readonly List<string> LastLines = new List<string>();
        /* Output text after the last line break.  */
        private readonly StringBuilder PartialLine = new StringBuilder();

        /* Note TEXT, added to the output while the buffer covers it: show
           its last lines in the strip and count them in the status line.  A
           line ends at CR, LF or both, as devices differ; empty lines are
           skipped.  */
        public void MarkOutput(string text)
        {
            foreach (char c in text)
            {
                if (c == '\n' || c == '\r')
                {
                    if (PartialLine.ToString().Trim().Length > 0)
                    {
                        NewLines++;
                        AddLastLine(PartialLine.ToString());
                    }
                    PartialLine.Clear();
                }
                else if (PartialLine.Length < 200)
                    PartialLine.Append(c);
            }

            var shown = new List<string>(LastLines);
            if (PartialLine.Length > 0)
                shown.Add(PartialLine.ToString());
            while (shown.Count > ReceivedLines)
                shown.RemoveAt(0);
            Received.ForeColor = TextColor;
            Received.Text = string.Join("\n", shown);
            UpdateStatus();
        }

        /* Keep LINE as one of the last lines of the output.  */
        private void AddLastLine(string line)
        {
            LastLines.Add(line);
            if (LastLines.Count > ReceivedLines)
                LastLines.RemoveAt(0);
        }

        /* Forget the output lines, as when the buffer is shown again.  */
        public void ResetReceived()
        {
            NewLines = 0;
            LastLines.Clear();
            PartialLine.Clear();
            Received.ForeColor = Dim;
            Received.Text = "Nothing new in the output since the buffer opened.";
        }

        /* Place the lines and their fields for the current width.  */
        private void LayoutRows()
        {
            /* Leave room for the vertical scroll bar whenever it may show,
               so that no horizontal one is ever needed.  */
            int width = RowsPanel.Width - 2;
            if (RowHeight * Rows.Length + 4 > RowsPanel.Height)
                width -= SystemInformation.VerticalScrollBarWidth;
            width = Math.Max(S(400), width);
            int labelWidth = Math.Max(S(110), width / 5);

            foreach (Row row in Rows)
            {
                row.Panel.SetBounds(0, S(2) + row.Index * RowHeight, width, RowHeight);
                int textTop = (RowHeight - row.Label.PreferredHeight) / 2 + 1;
                row.Number.SetBounds(0, 0, S(40), RowHeight);
                row.Label.SetBounds(S(50), textTop, labelWidth, row.Label.PreferredHeight);
                row.Bar.SetBounds(S(50) + labelWidth, 0, S(18), RowHeight);
                /* The repeat column sits between label and command, so
                   that it lines up on every line.  */
                int repeatLeft = S(72) + labelWidth + RepeatIconWidth;
                row.RepeatIcon.SetBounds(S(72) + labelWidth, 0, RepeatIconWidth, RowHeight);
                row.Repeat.SetBounds(repeatLeft, textTop, RepeatWidth, row.Repeat.PreferredHeight);
                row.Bar2.SetBounds(repeatLeft + RepeatWidth, 0, S(18), RowHeight);
                row.Command.SetBounds(repeatLeft + RepeatWidth + S(22), textTop, S(80), row.Command.PreferredHeight);
                FitTag(row);
            }
        }

        /*----------------------------------------------------------
         * Loading, saving and state
         *---------------------------------------------------------*/

        /* First page of the group of the page shown.  */
        private int GroupFirstPage =>
            Page / Settings.Layout.PagesPerGroup * Settings.Layout.PagesPerGroup;

        /* Number of the first macro on the page shown.  */
        private int FirstMacro => Page * Settings.Layout.ButtonsPerPage;

        /* Show page PAGE, an absolute page number, from the saved settings.
           Unsaved changes are dropped: call ConfirmLeave first.  */
        public void ShowPage(int page)
        {
            Page = Math.Max(0, Math.Min(page, Settings.Layout.Pages - 1));
            Loading = true;

            SavedPageName = Settings.Page.Text[Page] ?? "";
            PageName.Text = SavedPageName;

            foreach (Row row in Rows)
            {
                int macro = FirstMacro + row.Index;
                row.SavedLabel = Settings.MacroText.Text[macro] ?? "";
                row.SavedCommand = Settings.MacroCommand.Text[macro] ?? "";
                row.SavedRepeat = Settings.MacroRepeat.Interval[macro] ?? "";
                row.Label.Text = row.SavedLabel;
                row.Command.Text = row.SavedCommand;
                row.Repeat.Text = row.SavedRepeat;
                row.Origin = macro;
            }

            Loading = false;
            ClearUndo();
            if (ActiveControl is TextBox focused)
                FieldStart = focused.Text;
            Note = "";
            RefreshAll();
        }

        /* Throw away the unsaved changes.  */
        public void Revert()
        {
            ShowPage(Page);
            Note = "Reverted.";
            UpdateStatus();
        }

        /* Write the page to the settings and config.xml.  Return false,
           saving nothing, if a line has a problem.  */
        public bool Save()
        {
            Row bad = Rows.FirstOrDefault(r => ProblemOf(r) != null);
            if (bad != null)
            {
                Note = $"Fix line {bad.Index + 1:00} first: {ProblemOf(bad)}.";
                UpdateStatus();
                (bad.Label.Text.Trim().Length == 0 ? bad.Label : bad.Command).Focus();
                return false;
            }

            int count = ChangeCount();
            Settings.Page.Text[Page] = PageName.Text.Trim();
            foreach (Row row in Rows)
            {
                int macro = FirstMacro + row.Index;
                Settings.MacroText.Text[macro] = row.Label.Text.Trim();
                Settings.MacroCommand.Text[macro] = row.Command.Text.Trim();
                Settings.MacroRepeat.Interval[macro] = row.Command.Text.Trim().Length > 0 ? row.Repeat.Text.Trim() : "";
            }
            Settings.WriteXml();

            var moved = new Dictionary<int, int>();
            foreach (Row row in Rows)
                if (row.Origin >= 0)
                    moved[row.Origin] = FirstMacro + row.Index;
            Moved?.Invoke(moved);

            ShowPage(Page);
            Note = count == 1 ? "Saved 1 change." : $"Saved {count} changes.";
            UpdateStatus();
            Saved?.Invoke();
            return true;
        }

        /* If there are unsaved changes, ask whether to save them.  Return
           false if the user wants to stay, or saving failed.  */
        public bool ConfirmLeave()
        {
            if (!IsDirty)
                return true;

            string name = PageName.Text.Trim();
            DialogResult answer = AskSave($"Save the changes to page {Page + 1} {name}".TrimEnd() + "?");
            if (answer == DialogResult.Yes)
                return Save();
            if (answer == DialogResult.No)
            {
                ShowPage(Page);
                return true;
            }
            return false;
        }

        /* True if anything differs from the saved settings.  */
        public bool IsDirty => ChangeCount() > 0;

        /* Number of changed lines, counting a changed page name as one.  */
        private int ChangeCount()
        {
            int count = Rows.Count(IsChanged);
            if (PageName.Text.Trim() != SavedPageName.Trim())
                count++;
            return count;
        }

        /* True if ROW differs from its saved macro.  */
        private static bool IsChanged(Row row) =>
            row.Label.Text.Trim() != row.SavedLabel.Trim() ||
            row.Command.Text.Trim() != row.SavedCommand.Trim() ||
            row.Repeat.Text.Trim() != row.SavedRepeat.Trim();

        /* Return what is wrong with ROW, or null if nothing is.  */
        private static string ProblemOf(Row row)
        {
            bool hasLabel = row.Label.Text.Trim().Length > 0;
            bool hasCommand = row.Command.Text.Trim().Length > 0;
            string repeat = row.Repeat.Text.Trim();
            string command = row.Command.Text.Trim();
            if (repeat.Length > 0)
            {
                if (!int.TryParse(repeat, out int interval) || interval < 50)
                    return "repeat needs 50 ms or more";
                if (command.StartsWith(">", StringComparison.Ordinal))
                    return "scripts do not repeat";
                if (IsRisky(command))
                    return "risky commands do not repeat";
                if (!hasCommand)
                    return "needs a command";
            }
            if (hasLabel && !hasCommand)
                return "needs a command";
            if (hasCommand && !hasLabel)
                return "needs a label";
            return null;
        }

        /* True if COMMAND starts with one of the risky commands.  */
        internal static bool IsRisky(string command)
        {
            string text = command.Trim();
            if (text.Length == 0)
                return false;
            foreach (string risky in (Settings.MacroBuffer.RiskyCommands ?? "").Split(';'))
            {
                string prefix = risky.Trim();
                if (prefix.Length > 0 && text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /* Return how macro button BUTTON of the page shown should look: its
           LABEL, COMMAND and repeat INTERVAL (0 for none) as typed so far,
           and its STATE.  */
        public void Preview(int button, out string label, out string command, out int interval, out MacroState state)
        {
            Row row = Rows[button];
            label = row.Label.Text.Trim();
            command = row.Command.Text.Trim();
            interval = int.TryParse(row.Repeat.Text.Trim(), out int ms) && ms > 0 ? ms : 0;
            state = ProblemOf(row) != null ? MacroState.Problem
                  : IsChanged(row) ? MacroState.Changed
                  : MacroState.Saved;
        }

        /* Name of the page shown, as typed so far.  */
        public string PreviewPageName => PageName.Text.Trim();

        /*----------------------------------------------------------
         * Showing
         *---------------------------------------------------------*/

        /* Something was edited.  Typing ends the page-level undo: Ctrl+Z
           then undoes typing in the field again.  */
        private void Changed()
        {
            if (Loading)
                return;
            UndoIsPageLevel = false;
            Note = "";
            RefreshAll();
        }

        /*----------------------------------------------------------
         * Page-level undo
         *---------------------------------------------------------*/

        /* The page as it was before a move or a paste of several lines.  */
        private sealed class Snapshot
        {
            public string PageName;
            public string[] Labels, Commands, Repeats;
            public int[] Origins;
        }

        /* Snapshots to go back to, newest last.  */
        private readonly List<Snapshot> UndoStack = new List<Snapshot>();
        /* True while Ctrl+Z undoes moves and pastes rather than typing.  */
        private bool UndoIsPageLevel;

        /* Remember the page before a move or a paste.  */
        private void PushUndo()
        {
            UndoStack.Add(new Snapshot
            {
                PageName = PageName.Text,
                Labels = Rows.Select(r => r.Label.Text).ToArray(),
                Commands = Rows.Select(r => r.Command.Text).ToArray(),
                Repeats = Rows.Select(r => r.Repeat.Text).ToArray(),
                Origins = Rows.Select(r => r.Origin).ToArray()
            });
            if (UndoStack.Count > 50)
                UndoStack.RemoveAt(0);
        }

        /* After a move or a paste, let Ctrl+Z undo it.  */
        private void PageActionDone() => UndoIsPageLevel = true;

        /* Go back to the page before the last move or paste.  Return false
           if there is none to undo.  */
        private bool UndoPageAction()
        {
            if (!UndoIsPageLevel || UndoStack.Count == 0)
                return false;

            Snapshot snapshot = UndoStack[UndoStack.Count - 1];
            UndoStack.RemoveAt(UndoStack.Count - 1);
            Loading = true;
            PageName.Text = snapshot.PageName;
            for (int i = 0; i < Rows.Length; i++)
            {
                Rows[i].Label.Text = snapshot.Labels[i];
                Rows[i].Command.Text = snapshot.Commands[i];
                Rows[i].Repeat.Text = snapshot.Repeats[i];
                Rows[i].Origin = snapshot.Origins[i];
            }
            Loading = false;
            Changed();
            if (ActiveControl is TextBox focused)
                FieldStart = focused.Text;
            UndoIsPageLevel = UndoStack.Count > 0;
            Note = "Undone.";
            UpdateStatus();
            return true;
        }

        /* Forget what can be undone, as after loading or saving.  */
        private void ClearUndo()
        {
            UndoStack.Clear();
            UndoIsPageLevel = false;
        }

        /* Bring every line, the header and the status line up to date, and
           tell the main window.  */
        private void RefreshAll()
        {
            foreach (Row row in Rows)
                UpdateRow(row);
            UpdateHeader();
            UpdateStatus();
            Edited?.Invoke();
        }

        /* Color ROW and set its tag from its state.  */
        private void UpdateRow(Row row)
        {
            string command = row.Command.Text.Trim();
            string problem = ProblemOf(row);
            bool changed = IsChanged(row);
            bool script = command.StartsWith(">load", StringComparison.Ordinal);
            bool risky = IsRisky(command);
            bool empty = row.Label.Text.Trim().Length == 0 && command.Length == 0;

            var tags = new List<string>();
            if (problem != null) tags.Add(problem);
            if (changed) tags.Add("changed");
            if (risky) tags.Add("risky command");
            if (script) tags.Add("script");
            if (empty && !changed) tags.Add("empty");
            row.Tag.Text = string.Join(" · ", tags);
            row.Tag.ForeColor = problem != null ? Red : changed ? Amber : risky ? Orange : script ? Violet : Dim;
            FitTag(row);

            Color back = row.Index == CurrentRow ? CurrentBack : changed ? ChangedBack : Back;
            row.Panel.BackColor = back;
            row.Label.BackColor = back;
            row.Command.BackColor = back;
            row.Repeat.BackColor = back;
            row.RepeatIcon.BackColor = back;
            row.RepeatIcon.ForeColor = row.Repeat.Text.Trim().Length > 0 ? Sky : Faint;
            row.Tag.BackColor = back;

            row.Label.ForeColor = TextColor;
            row.Command.ForeColor = script ? Violet : TextColor;
            row.Number.ForeColor = changed ? Amber : Dim;
        }

        /* Size the tag of ROW to its text, and give the command field the
           rest of the line.  */
        private void FitTag(Row row)
        {
            int width = row.Panel.Width;
            int tagWidth = row.Tag.Text.Length == 0 ? 0
                         : TextRenderer.MeasureText(row.Tag.Text, row.Tag.Font).Width + S(8);
            row.Tag.SetBounds(width - tagWidth - S(10), 0, tagWidth, RowHeight);
            row.Command.Width = Math.Max(S(80), width - row.Command.Left - tagWidth - S(16));
        }

        /* Label the page chips and the group switch, marking the page
           shown.  */
        private void UpdateHeader()
        {
            for (int i = 0; i < Chips.Length; i++)
            {
                int page = GroupFirstPage + i;
                string name = page == Page ? PageName.Text.Trim() : (Settings.Page.Text[page] ?? "").Trim();
                bool active = page == Page;
                /* Only the page shown has its name on the chip, so that
                   all of them fit; the others show it as a tip.  */
                FitText(Chips[i], active && name.Length > 0 ? $"{i + 1} {name}" : $"{i + 1}");
                ChipTips.SetToolTip(Chips[i], name.Length > 0 ? name : $"Page {i + 1}");
                Chips[i].BackColor = active ? Color.FromArgb(0x13, 0x4E, 0x4A) : ChipBack;
                Chips[i].ForeColor = active ? Color.FromArgb(0xCC, 0xFB, 0xF1) : TextColor;
                Chips[i].FlatAppearance.BorderColor = active ? Color.FromArgb(0x5E, 0xEA, 0xD4) : ChipBorder;
            }

            int group = Page / Settings.Layout.PagesPerGroup;
            FitText(GroupButton, $"Group {(char)('A' + group)} \u2194");
            GroupButton.Visible = Settings.Layout.Groups > 1;

            int count = ChangeCount();
            FitText(SaveButton, count == 0 ? "Save" : count == 1 ? "Save 1 change" : $"Save {count} changes");
            SaveButton.BackColor = count == 0 ? ChipBack : Color.FromArgb(0xF5, 0x9E, 0x0B);
            SaveButton.ForeColor = count == 0 ? Dim : Color.FromArgb(0x1F, 0x13, 0x00);
            RevertButton.Enabled = count > 0;

            int first = FirstMacro;
            Hint.Text = $"macros {first}–{first + Rows.Length - 1} · label │ ~ repeat ms │ command";
        }

        /* Write the Emacs-style status line.  */
        private void UpdateStatus()
        {
            int changes = ChangeCount();
            int problems = Rows.Count(r => ProblemOf(r) != null);
            int group = Page / Settings.Layout.PagesPerGroup;
            var text = new StringBuilder();
            text.Append(changes > 0 ? "-:**-  " : "-:---  ");
            text.Append($"*macros*   Group {(char)('A' + group)} · Page {Page % Settings.Layout.PagesPerGroup + 1} {PageName.Text.Trim()} · {FirstMacro}–{FirstMacro + Rows.Length - 1}");
            if (CurrentRow >= 0)
                text.Append($"   L{CurrentRow + 1}");
            if (changes > 0)
                text.Append($"   {changes} unsaved");
            if (problems > 0)
                text.Append(problems == 1 ? "   1 problem" : $"   {problems} problems");
            if (Note.Length > 0)
                text.Append("   " + Note);
            if (NewLines > 0)
                text.Append(NewLines == 1 ? "   \u25BC 1 new line" : $"   \u25BC {NewLines} new lines");
            text.Append("      Ctrl+S save · Ctrl+Enter send line · Alt+↑↓ move · Ctrl+Z undo · Esc close");
            Status.Text = text.ToString();
            Status.ForeColor = problems > 0 && Note.StartsWith("Fix") ? Red : StatusText;
        }

        /* Make line ROW (or -1 for none) the current line.  */
        private void SetCurrentRow(int row)
        {
            int old = CurrentRow;
            CurrentRow = row;
            if (old >= 0 && old < Rows.Length)
                UpdateRow(Rows[old]);
            if (row >= 0)
                UpdateRow(Rows[row]);
            UpdateStatus();
        }

        /* Put the cursor on the label of macro button BUTTON.  */
        public void FocusRow(int button)
        {
            if (button >= 0 && button < Rows.Length)
            {
                Rows[button].Label.Focus();
                Rows[button].Label.SelectAll();
                RowsPanel.ScrollControlIntoView(Rows[button].Panel);
            }
        }

        /*----------------------------------------------------------
         * Editing
         *---------------------------------------------------------*/

        /* Ask the main window for page PAGE.  */
        private void RequestPage(int page)
        {
            if (page != Page)
                PageRequested?.Invoke(page);
        }

        /* Swap the macro of line INDEX with the one DELTA lines away and
           follow it, keeping the cursor in field COLUMN.  */
        private void MoveRow(int index, int delta, int column)
        {
            int other = index + delta;
            if (other < 0 || other >= Rows.Length)
                return;

            Row a = Rows[index], b = Rows[other];
            PushUndo();
            Loading = true;
            string label = a.Label.Text, command = a.Command.Text, repeat = a.Repeat.Text;
            a.Label.Text = b.Label.Text; a.Command.Text = b.Command.Text; a.Repeat.Text = b.Repeat.Text;
            b.Label.Text = label; b.Command.Text = command; b.Repeat.Text = repeat;
            int origin = a.Origin; a.Origin = b.Origin; b.Origin = origin;
            Loading = false;

            b.Fields[column].Focus();
            Changed();
            FieldStart = b.Fields[column].Text;
            PageActionDone();
        }

        /* Return the page as text, one "label | command" line per macro.  */
        private string PageText()
        {
            var text = new StringBuilder();
            foreach (Row row in Rows)
                text.Append(row.Label.Text.Trim()).Append(" | ").Append(row.Command.Text.Trim()).Append("\r\n");
            return text.ToString();
        }

        /* Fill lines from FIRST down with the "label | command" lines of
           TEXT.  A line without "|" or "│" is a command only.  */
        private void PasteLines(int first, string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            int count = lines.Length;
            if (count > 0 && lines[count - 1].Length == 0)
                count--;

            PushUndo();
            Loading = true;
            for (int i = 0; i < count && first + i < Rows.Length; i++)
            {
                string line = lines[i];
                int bar = line.IndexOfAny(new[] { '|', '│' });
                Row row = Rows[first + i];
                /* A pasted macro is a new one: a repeat of the old one
                   stops when this is saved.  */
                row.Origin = -1;
                if (bar >= 0)
                {
                    row.Label.Text = line.Substring(0, bar).Trim();
                    row.Command.Text = line.Substring(bar + 1).Trim();
                }
                else
                {
                    row.Command.Text = line.Trim();
                }
            }
            Loading = false;
            Changed();
            if (ActiveControl is TextBox focused)
                FieldStart = focused.Text;
            PageActionDone();
        }

        /* Handle the buffer's keys before the main window's menus see them,
           so that Ctrl+C, Ctrl+V, Delete and the like edit the field instead
           of the output window.  */
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            TextBox box = ActiveControl as TextBox;
            Row row = box == null ? null : Rows.FirstOrDefault(r => r.Fields.Contains(box));
            /* Column of the field with the focus: 0 label, 1 command, 2
               repeat.  */
            int column = row == null ? 0 : Array.IndexOf(row.Fields, box);

            switch (keyData)
            {
                case Keys.Control | Keys.S:
                    Save();
                    return true;

                case Keys.Escape:
                    /* First Esc gives the field back the text it had;
                       Esc in an unchanged field closes the buffer.  */
                    if (box != null && box.Text != FieldStart)
                    {
                        box.Text = FieldStart;
                        box.SelectAll();
                        Note = "Field restored.";
                        UpdateStatus();
                    }
                    else
                        CloseRequested?.Invoke();
                    return true;

                case Keys.Control | Keys.Enter:
                    if (row != null && row.Command.Text.Trim().Length > 0)
                        SendRequested?.Invoke(row.Command.Text.Trim());
                    return true;

                case Keys.Enter:
                    if (box == PageName)
                        Rows[0].Label.Focus();
                    else if (row != null && column < 2)
                        row.Fields[column + 1].Focus();
                    else if (row != null && row.Index + 1 < Rows.Length)
                        Rows[row.Index + 1].Label.Focus();
                    return true;

                case Keys.Up:
                case Keys.Down:
                    if (row != null)
                    {
                        int next = row.Index + (keyData == Keys.Up ? -1 : 1);
                        if (next >= 0 && next < Rows.Length)
                            Rows[next].Fields[column].Focus();
                        return true;
                    }
                    break;

                case Keys.Alt | Keys.Up:
                case Keys.Alt | Keys.Down:
                    if (row != null)
                        MoveRow(row.Index, keyData == (Keys.Alt | Keys.Up) ? -1 : 1, column);
                    return true;

                case Keys.Control | Keys.Shift | Keys.C:
                    try { Clipboard.SetText(PageText()); } catch (System.Runtime.InteropServices.ExternalException) { }
                    Note = "Page copied as text.";
                    UpdateStatus();
                    return true;

                case Keys.Control | Keys.V:
                case Keys.Shift | Keys.Insert:
                    string clip = "";
                    try { clip = Clipboard.GetText(); } catch (System.Runtime.InteropServices.ExternalException) { }
                    if (row != null && clip.IndexOf('\n') >= 0)
                    {
                        PasteLines(row.Index, clip);
                        return true;
                    }
                    return false;

                case Keys.Control | Keys.A:
                    if (box != null)
                        box.SelectAll();
                    return true;

                case Keys.Control | Keys.Z:
                    /* Undo a move or a paste; otherwise let the field undo
                       its typing.  */
                    return UndoPageAction();

                case Keys.Control | Keys.C:
                case Keys.Control | Keys.X:
                case Keys.Control | Keys.Insert:
                case Keys.Shift | Keys.Delete:
                case Keys.Delete:
                case Keys.Control | Keys.Delete:
                    /* Let the field handle them; don't pass them to the
                       window's menu.  */
                    return false;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
