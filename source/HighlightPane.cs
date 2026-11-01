/* HighlightPane.cs -- the highlight editor: the rules that color output
   lines, edited like the macro buffer, over the output window.

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace _4Term
{
    /* The highlight editor.  It shows the Settings.Highlight.Count rules as
       lines: number, color dot, the text to look for, written in its
       color, and whether to color the whole line or only the text.  An
       output line that contains the text, ignoring case, is colored: for
       whole-line rules the first that matches wins, so the order matters;
       word rules color every match on top.  Edits stay here until Save.

       Keys: Enter and the arrow keys move between lines, Ctrl+Space
       switches between line and word, Alt+Up and Alt+Down move a rule,
       Ctrl+S saves, Esc closes.  Click a dot to choose its color.  */
    internal sealed class HighlightPane : UserControl
    {
        /* Asks to close the editor.  */
        public event Action CloseRequested;
        /* Raised after the rules were saved to the settings.  */
        public event Action Saved;

        /* Asks QUESTION about unsaved changes; Yes saves, No discards,
           Cancel stays.  Replaceable for tests.  */
        internal Func<string, DialogResult> AskSave = question =>
            MessageBox.Show(question, "Highlights", MessageBoxButtons.YesNoCancel,
                            MessageBoxIcon.Question);

        /* Colors offered for rules, in menu order: they read well on dark
           and on light output backgrounds.  */
        internal static readonly KeyValuePair<string, Color>[] Presets =
        {
            new KeyValuePair<string, Color>("Red",    Color.FromArgb(0xFF, 0x55, 0x55)),
            new KeyValuePair<string, Color>("Orange", Color.FromArgb(0xFF, 0x9F, 0x1C)),
            new KeyValuePair<string, Color>("Yellow", Color.FromArgb(0xE6, 0xC2, 0x00)),
            new KeyValuePair<string, Color>("Green",  Color.FromArgb(0x3D, 0xC8, 0x5A)),
            new KeyValuePair<string, Color>("Cyan",   Color.FromArgb(0x22, 0xB8, 0xCF)),
            new KeyValuePair<string, Color>("Blue",   Color.FromArgb(0x4D, 0x8D, 0xFF)),
            new KeyValuePair<string, Color>("Violet", Color.FromArgb(0xA7, 0x6B, 0xFF)),
            new KeyValuePair<string, Color>("Pink",   Color.FromArgb(0xF0, 0x5C, 0xA8)),
        };

        /* Return COLOR, "#RRGGBB", as a color; red if it is not one.  */
        public static Color ParseColor(string color)
        {
            string text = (color ?? "").Trim().TrimStart('#');
            if (text.Length == 6 && int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            return Presets[0].Value;
        }

        /* True if MODE, as stored in the settings, means coloring only the
           matching text.  */
        public static bool IsWordMode(string mode) =>
            string.Equals((mode ?? "").Trim(), "word", StringComparison.OrdinalIgnoreCase);

        /* Return COLOR as "#RRGGBB".  */
        public static string FormatColor(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        /*----------------------------------------------------------
         * Look
         *---------------------------------------------------------*/

        private static readonly Color Back = Color.FromArgb(0x16, 0x18, 0x1D);
        private static readonly Color HeaderBack = Color.FromArgb(0x1C, 0x20, 0x28);
        private static readonly Color CurrentBack = Color.FromArgb(0x22, 0x28, 0x33);
        private static readonly Color ChangedBack = Color.FromArgb(0x2A, 0x24, 0x1B);
        private static readonly Color TextColor = Color.FromArgb(0xE5, 0xE7, 0xEB);
        private static readonly Color Dim = Color.FromArgb(0x8A, 0x93, 0xA3);
        private static readonly Color Faint = Color.FromArgb(0x4B, 0x55, 0x63);
        private static readonly Color Amber = Color.FromArgb(0xFC, 0xD3, 0x4D);
        private static readonly Color ChipBack = Color.FromArgb(0x20, 0x25, 0x2E);
        private static readonly Color ChipBorder = Color.FromArgb(0x34, 0x3B, 0x48);
        private static readonly Color StatusBack = Color.FromArgb(0x2A, 0x2F, 0x3A);
        private static readonly Color StatusText = Color.FromArgb(0xD8, 0xDE, 0xE9);

        /* Screen pixels per design pixel, as in the macro buffer.  */
        private static readonly float DpiScale = ReadDpiScale();

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

        /*----------------------------------------------------------
         * Lines
         *---------------------------------------------------------*/

        /* One rule.  */
        private sealed class Row
        {
            public int Index;
            public Panel Panel;
            public Label Number;
            public Button Dot;
            public TextBox Text;
            /* Divider, and the switch between line and word.  */
            public Label Bar;
            public Button Mode;
            public Label Tag;
            /* The color and mode now, and the saved text, color and mode.  */
            public Color Color;
            public bool Word;
            public string SavedText = "";
            public Color SavedColor;
            public bool SavedWord;
        }

        private readonly Row[] Rows = new Row[Settings.Highlight.Count];
        private readonly Panel Header, RowsPanel;
        private readonly Label Title, Hint, Status;
        private readonly Button RevertButton, SaveButton, CloseButton;
        private readonly ContextMenuStrip ColorMenu = new ContextMenuStrip();
        private readonly Font UiFont = new Font("Segoe UI", 9f);
        private readonly Font UiBold = new Font("Segoe UI", 9f, FontStyle.Bold);
        /* Consolas, by name: a font made from FontFamily.GenericMonospace
           breaks on Windows once that temporary family is collected, with
           "Parameter is not valid" when a text box is created.  */
        private readonly Font EditorFont = new Font("Consolas", 10f);
        private readonly int RowHeight;
        /* Line with the cursor, and the line whose color menu is open.  */
        private int CurrentRow;
        private Row MenuRow;
        /* True while lines are being filled, so no change is reported.  */
        private bool Loading;
        /* One-time note in the status line.  */
        private string Note = "";

        /* Create the editor.  Call LoadRules to fill it.  */
        public HighlightPane()
        {
            BackColor = Back;
            ForeColor = TextColor;
            Font = UiFont;
            RowHeight = Math.Max(S(22), EditorFont.Height + S(6));

            /* Header: title and hint, Revert, Save and Close.  */
            Header = new Panel { Dock = DockStyle.Top, Height = S(34), BackColor = HeaderBack };
            Title = new Label { Text = "HIGHLIGHTS", AutoSize = true, ForeColor = Dim, Font = UiBold, Location = new Point(S(10), S(9)) };
            Hint = new Label { AutoSize = true, ForeColor = Dim, Location = new Point(S(110), S(9)),
                               Text = "text │ line: color the line, first match wins · word: color only the text" };
            RevertButton = MakeButton("Revert", (s, e) => Revert());
            SaveButton = MakeButton("Save", (s, e) => Save());
            CloseButton = MakeButton("Close", (s, e) => CloseRequested?.Invoke());
            Header.Controls.AddRange(new Control[] { Title, Hint, RevertButton, SaveButton, CloseButton });
            Header.Resize += (s, e) => LayoutHeader();

            /* The rule lines.  */
            RowsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Back, AutoScroll = true };
            for (int i = 0; i < Rows.Length; i++)
                Rows[i] = MakeRow(i);
            RowsPanel.Resize += (s, e) => LayoutRows();

            Status = new Label { Dock = DockStyle.Bottom, Height = S(22), BackColor = StatusBack, ForeColor = StatusText,
                                 TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(S(8), 0, 0, 0) };

            /* Docked controls are laid out in reverse order of adding.  */
            Controls.Add(RowsPanel);
            Controls.Add(Status);
            Controls.Add(Header);

            /* Color menu: the presets, then a color of one's own.  */
            foreach (KeyValuePair<string, Color> preset in Presets)
            {
                Color color = preset.Value;
                ColorMenu.Items.Add(preset.Key, SwatchImage(color), (s, e) => SetColor(MenuRow, color));
            }
            ColorMenu.Items.Add(new ToolStripSeparator());
            ColorMenu.Items.Add("Other...", null, (s, e) => ChooseColor(MenuRow));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ColorMenu.Dispose();
                UiFont.Dispose();
                UiBold.Dispose();
                EditorFont.Dispose();
            }
            base.Dispose(disposing);
        }

        /* Return a flat header button with TEXT that runs CLICK.  */
        private Button MakeButton(string text, EventHandler click)
        {
            var button = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = ChipBack,
                ForeColor = TextColor,
                Height = S(26),
                TabStop = false
            };
            button.FlatAppearance.BorderColor = ChipBorder;
            button.Click += click;
            FitText(button, text);
            return button;
        }

        /* Set the text of BUTTON to TEXT and its width to fit it.  */
        private static void FitText(Button button, string text)
        {
            button.Text = text;
            button.Width = Math.Max(S(28), TextRenderer.MeasureText(text, button.Font).Width + S(14));
        }

        /* Return line INDEX with its controls, added to RowsPanel.  */
        private Row MakeRow(int index)
        {
            var row = new Row { Index = index, Color = Presets[0].Value, SavedColor = Presets[0].Value };
            row.Panel = new Panel { BackColor = Back };
            row.Number = new Label { Text = (index + 1).ToString("00"), ForeColor = Dim, TextAlign = ContentAlignment.MiddleRight, Font = EditorFont };
            row.Dot = new Button { FlatStyle = FlatStyle.Flat, TabStop = false, BackColor = Back };
            row.Dot.FlatAppearance.BorderSize = 0;
            row.Dot.Paint += (s, e) => PaintDot(row, e.Graphics);
            row.Dot.Click += (s, e) =>
            {
                MenuRow = row;
                ColorMenu.Show(row.Dot, new Point(0, row.Dot.Height));
            };
            row.Text = new TextBox { BorderStyle = BorderStyle.None, BackColor = Back, ForeColor = TextColor, Font = EditorFont, TabIndex = index };
            row.Bar = new Label { Text = "│", ForeColor = Faint, TextAlign = ContentAlignment.MiddleCenter, Font = EditorFont };
            row.Mode = new Button { FlatStyle = FlatStyle.Flat, TabStop = false, BackColor = Back, ForeColor = Dim, Font = UiFont, TextAlign = ContentAlignment.MiddleLeft };
            row.Mode.FlatAppearance.BorderSize = 0;
            row.Mode.Click += (s, e) => { ToggleMode(row); row.Text.Focus(); };
            row.Tag = new Label { TextAlign = ContentAlignment.MiddleRight, Font = UiFont };

            row.Text.TextChanged += (s, e) => Changed();
            row.Text.Enter += (s, e) => SetCurrentRow(index);

            row.Panel.Controls.AddRange(new Control[] { row.Number, row.Dot, row.Text, row.Bar, row.Mode, row.Tag });
            RowsPanel.Controls.Add(row.Panel);
            return row;
        }

        /* Draw the color dot of ROW.  */
        private void PaintDot(Row row, Graphics g)
        {
            g.Clear(row.Panel.BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int size = Math.Min(row.Dot.Width, row.Dot.Height) - S(8);
            var circle = new Rectangle((row.Dot.Width - size) / 2, (row.Dot.Height - size) / 2, size, size);
            using (var brush = new SolidBrush(row.Color))
                g.FillEllipse(brush, circle);
        }

        /* Return a small square image in COLOR for the color menu.  */
        private static Image SwatchImage(Color color)
        {
            var image = new Bitmap(12, 12);
            using (Graphics g = Graphics.FromImage(image))
                g.Clear(color);
            return image;
        }

        /* Place the header buttons for the current width.  */
        private void LayoutHeader()
        {
            int right = Header.ClientSize.Width - S(8);
            foreach (Button button in new[] { CloseButton, SaveButton, RevertButton })
            {
                right -= button.Width;
                button.Location = new Point(right, S(4));
                right -= S(4);
            }
            Hint.Visible = Hint.Right < right;
        }

        /* Place the lines and their fields for the current width.  */
        private void LayoutRows()
        {
            int width = RowsPanel.Width - 2;
            if (RowHeight * Rows.Length + S(4) > RowsPanel.Height)
                width -= SystemInformation.VerticalScrollBarWidth;
            width = Math.Max(S(300), width);
            int tagWidth = S(150);

            foreach (Row row in Rows)
            {
                row.Panel.SetBounds(0, S(2) + row.Index * RowHeight, width, RowHeight);
                int textTop = (RowHeight - row.Text.PreferredHeight) / 2 + 1;
                row.Number.SetBounds(0, 0, S(40), RowHeight);
                row.Dot.SetBounds(S(46), 0, S(22), RowHeight);
                int modeWidth = S(54), barWidth = S(18);
                int textWidth = Math.Max(S(80), width - S(74) - barWidth - modeWidth - tagWidth - S(14));
                row.Text.SetBounds(S(74), textTop, textWidth, row.Text.PreferredHeight);
                row.Bar.SetBounds(S(74) + textWidth, 0, barWidth, RowHeight);
                row.Mode.SetBounds(S(74) + textWidth + barWidth, 0, modeWidth, RowHeight);
                row.Tag.SetBounds(width - tagWidth - S(10), 0, tagWidth, RowHeight);
            }
        }

        /*----------------------------------------------------------
         * Loading, saving and state
         *---------------------------------------------------------*/

        /* Show the rules from the settings.  Unsaved changes are dropped:
           call ConfirmLeave first.  */
        public void LoadRules()
        {
            Loading = true;
            foreach (Row row in Rows)
            {
                row.SavedText = Settings.Highlight.Text[row.Index] ?? "";
                row.SavedColor = ParseColor(Settings.Highlight.Color[row.Index]);
                row.SavedWord = IsWordMode(Settings.Highlight.Mode[row.Index]);
                row.Text.Text = row.SavedText;
                row.Color = row.SavedColor;
                row.Word = row.SavedWord;
            }
            Loading = false;
            Note = "";
            RefreshAll();
        }

        /* Throw away the unsaved changes.  */
        public void Revert()
        {
            LoadRules();
            Note = "Reverted.";
            UpdateStatus();
        }

        /* Write the rules to the settings and config.xml.  */
        public void Save()
        {
            int count = ChangeCount();
            foreach (Row row in Rows)
            {
                string text = row.Text.Text.Trim();
                Settings.Highlight.Text[row.Index] = text;
                Settings.Highlight.Color[row.Index] = text.Length > 0 ? FormatColor(row.Color) : "";
                Settings.Highlight.Mode[row.Index] = text.Length == 0 ? "" : row.Word ? "word" : "line";
            }
            Settings.WriteXml();

            LoadRules();
            Note = count == 1 ? "Saved 1 change." : $"Saved {count} changes.";
            UpdateStatus();
            Saved?.Invoke();
        }

        /* If there are unsaved changes, ask whether to save them.  Return
           false if the user wants to stay.  */
        public bool ConfirmLeave()
        {
            if (!IsDirty)
                return true;
            DialogResult answer = AskSave("Save the changes to the highlights?");
            if (answer == DialogResult.Yes)
            {
                Save();
                return true;
            }
            if (answer == DialogResult.No)
            {
                LoadRules();
                return true;
            }
            return false;
        }

        /* True if anything differs from the saved settings.  */
        public bool IsDirty => ChangeCount() > 0;

        /* Number of changed lines.  */
        private int ChangeCount() => Rows.Count(IsChanged);

        /* True if ROW differs from its saved rule.  The color of a line
           without text does not count.  */
        private static bool IsChanged(Row row)
        {
            string text = row.Text.Text.Trim();
            if (text != row.SavedText.Trim())
                return true;
            return text.Length > 0 && (row.Color.ToArgb() != row.SavedColor.ToArgb() || row.Word != row.SavedWord);
        }

        /* Index of an earlier line with the same text as ROW, which makes
           ROW's rule never used, or -1.  */
        private int EarlierSame(Row row)
        {
            string text = row.Text.Text.Trim();
            if (text.Length == 0)
                return -1;
            for (int i = 0; i < row.Index; i++)
                if (string.Equals(Rows[i].Text.Text.Trim(), text, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        /*----------------------------------------------------------
         * Showing
         *---------------------------------------------------------*/

        /* Something was edited.  */
        private void Changed()
        {
            if (Loading)
                return;
            Note = "";
            RefreshAll();
        }

        /* Bring every line, the buttons and the status line up to date.  */
        private void RefreshAll()
        {
            foreach (Row row in Rows)
                UpdateRow(row);

            int count = ChangeCount();
            FitText(SaveButton, count == 0 ? "Save" : count == 1 ? "Save 1 change" : $"Save {count} changes");
            SaveButton.BackColor = count == 0 ? ChipBack : Color.FromArgb(0xF5, 0x9E, 0x0B);
            SaveButton.ForeColor = count == 0 ? Dim : Color.FromArgb(0x1F, 0x13, 0x00);
            RevertButton.Enabled = count > 0;
            LayoutHeader();
            UpdateStatus();
        }

        /* Show ROW in its color and set its tag.  */
        private void UpdateRow(Row row)
        {
            bool changed = IsChanged(row);
            bool empty = row.Text.Text.Trim().Length == 0;
            int same = EarlierSame(row);

            var tags = new List<string>();
            if (changed) tags.Add("changed");
            if (same >= 0) tags.Add($"same as {same + 1:00}");
            row.Tag.Text = string.Join(" · ", tags);
            row.Tag.ForeColor = same >= 0 ? Dim : Amber;

            Color back = row.Index == CurrentRow ? CurrentBack : changed ? ChangedBack : Back;
            row.Panel.BackColor = back;
            row.Text.BackColor = back;
            row.Tag.BackColor = back;
            row.Text.ForeColor = empty ? TextColor : row.Color;
            row.Mode.Text = row.Word ? "word" : "line";
            row.Mode.BackColor = back;
            row.Mode.ForeColor = row.Word ? Color.FromArgb(0x7D, 0xD3, 0xFC) : Dim;
            row.Mode.Visible = !empty || row.Index == CurrentRow;
            row.Number.ForeColor = changed ? Amber : Dim;
            row.Dot.Visible = !empty || row.Index == CurrentRow;
            row.Dot.Invalidate();
        }

        /* Write the status line.  */
        private void UpdateStatus()
        {
            int used = Rows.Count(r => r.Text.Text.Trim().Length > 0);
            int changes = ChangeCount();
            var text = new StringBuilder();
            text.Append(changes > 0 ? "-:**-  " : "-:---  ");
            text.Append($"*highlights*   {used} of {Rows.Length} used");
            if (CurrentRow >= 0)
                text.Append($"   L{CurrentRow + 1}");
            if (changes > 0)
                text.Append($"   {changes} unsaved");
            if (Note.Length > 0)
                text.Append("   " + Note);
            text.Append("      Ctrl+S save · Ctrl+Space line/word · Alt+↑↓ move · click the dot for a color · Esc close");
            Status.Text = text.ToString();
        }

        /* Make line ROW the current line.  */
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

        /* Put the cursor on line ROW.  */
        public void FocusRow(int row)
        {
            if (row < 0 || row >= Rows.Length)
                return;
            Rows[row].Text.Focus();
            Rows[row].Text.SelectAll();
            RowsPanel.ScrollControlIntoView(Rows[row].Panel);
        }

        /*----------------------------------------------------------
         * Editing
         *---------------------------------------------------------*/

        /* Give ROW the color COLOR.  */
        private void SetColor(Row row, Color color)
        {
            if (row == null)
                return;
            row.Color = color;
            Changed();
        }

        /* Switch ROW between coloring the whole line and only the text.  */
        private void ToggleMode(Row row)
        {
            if (row == null)
                return;
            row.Word = !row.Word;
            Changed();
        }

        /* Let the user choose any color for ROW.  */
        private void ChooseColor(Row row)
        {
            if (row == null)
                return;
            using (var dialog = new ColorDialog { Color = row.Color, FullOpen = true })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    SetColor(row, dialog.Color);
        }

        /* Swap the rule of line INDEX with the one DELTA lines away and
           follow it.  */
        private void MoveRow(int index, int delta)
        {
            int other = index + delta;
            if (other < 0 || other >= Rows.Length)
                return;
            Row a = Rows[index], b = Rows[other];
            Loading = true;
            string text = a.Text.Text;
            Color color = a.Color;
            bool word = a.Word;
            a.Text.Text = b.Text.Text;
            a.Color = b.Color;
            a.Word = b.Word;
            b.Text.Text = text;
            b.Color = color;
            b.Word = word;
            Loading = false;
            b.Text.Focus();
            Changed();
        }

        /* Handle the editor's keys before the main window's menus see them,
           so that Ctrl+C, Ctrl+V, Delete and the like edit the field.  */
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            TextBox box = ActiveControl as TextBox;
            Row row = box == null ? null : Rows.FirstOrDefault(r => r.Text == box);

            switch (keyData)
            {
                case Keys.Control | Keys.S:
                    Save();
                    return true;

                case Keys.Escape:
                    CloseRequested?.Invoke();
                    return true;

                case Keys.Enter:
                case Keys.Down:
                case Keys.Up:
                    if (row != null)
                    {
                        int next = row.Index + (keyData == Keys.Up ? -1 : 1);
                        if (next >= 0 && next < Rows.Length)
                            FocusRow(next);
                        return true;
                    }
                    break;

                case Keys.Alt | Keys.Up:
                case Keys.Alt | Keys.Down:
                    if (row != null)
                        MoveRow(row.Index, keyData == (Keys.Alt | Keys.Up) ? -1 : 1);
                    return true;

                case Keys.Control | Keys.Space:
                    ToggleMode(row);
                    return true;

                case Keys.Control | Keys.A:
                    if (box != null)
                        box.SelectAll();
                    return true;

                case Keys.Control | Keys.C:
                case Keys.Control | Keys.V:
                case Keys.Control | Keys.X:
                case Keys.Control | Keys.Z:
                case Keys.Control | Keys.Insert:
                case Keys.Shift | Keys.Insert:
                case Keys.Shift | Keys.Delete:
                case Keys.Delete:
                case Keys.Control | Keys.Delete:
                    return false;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
