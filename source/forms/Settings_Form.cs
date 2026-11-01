/* Settings_Form.cs -- the settings dialog: window size, display options, font
   and colors.

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

using System;
using System.Drawing;
using System.Windows.Forms;

namespace _4Term
{
    /* The settings dialog.  */
    public partial class Settings_Form : Form
    {
        /* Working copies of the colors and font, written to the settings only
           on Save, so that closing the dialog discards them.  */

        private Color BackColorValue;
        private Color TransmitColorValue;
        private Color ReceiveColorValue;
        private string FontName = Settings.Output.Font;
        private float FontSize = Settings.Output.Size;
        private string FontStyleName = Settings.Output.FontStyle;
        private bool FontStrikeout = Settings.Output.Strikeout;
        private bool FontUnderline = Settings.Output.Underline;

        /* Create the dialog and show the current settings in it.  */
        public Settings_Form()
        {
            InitializeComponent();

            BackColorValue = TupleToColor(Settings.Colors.Back);
            TransmitColorValue = TupleToColor(Settings.Colors.Transmit);
            ReceiveColorValue = TupleToColor(Settings.Colors.Receive);

            LoadSettings();
        }

        /* Store the dialog's values in the settings, save them and close the
           dialog with OK.  */
        private void SaveSettingsButton_Click(object sender, EventArgs e)
        {
            /* Program.  */
            Settings.Program.ShowWelcomeMessage = ShowWelcomeMessageCheckBox.Checked;

            /* Form.  */
            if (int.TryParse(FormWidthTextBox.Text, out int width))
                Settings.Window.Width = width;
            if (int.TryParse(FormHeightTextBox.Text, out int height))
                Settings.Window.Height = height;

            /* RichTextBox.  */
            Settings.Output.WordWrap = WordWrapCheckBox.Checked;
            Settings.Output.Scroll = ToggleScrollingCheckBox.Checked;
            Settings.Output.HexOutput = HexOutputCheckBox.Checked;
            Settings.Output.LocalEcho = LocalEchoCheckBox.Checked;
            Settings.Output.Timestamps = TimestampsCheckBox.Checked;

            /* Log.  */
            Settings.Log.Enabled = LogCheckBox.Checked && LogPathTextBox.Text.Trim().Length > 0;
            Settings.Log.Path = LogPathTextBox.Text.Trim();

            /* Font.  */
            Settings.Output.Font = FontName;
            Settings.Output.Size = FontSize;
            Settings.Output.FontStyle = FontStyleName;
            Settings.Output.Strikeout = FontStrikeout;
            Settings.Output.Underline = FontUnderline;

            /* Colors.  */
            Settings.Colors.Back = (BackColorValue.R, BackColorValue.G, BackColorValue.B);
            Settings.Colors.Transmit = (TransmitColorValue.R, TransmitColorValue.G, TransmitColorValue.B);
            Settings.Colors.Receive = (ReceiveColorValue.R, ReceiveColorValue.G, ReceiveColorValue.B);

            /* Save changes.  */
            Settings.WriteXml();

            /* Close form, reporting that settings were saved.  */
            DialogResult = DialogResult.OK;
        }

        /* Let the user choose the log file.  Choosing one turns logging
           on.  */
        private void LogBrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog
            {
                Title = "Log File",
                Filter = "Log files (*.log;*.txt)|*.log;*.txt|All files (*.*)|*.*",
                OverwritePrompt = false,
                FileName = LogPathTextBox.Text.Trim().Length > 0
                    ? System.IO.Path.GetFileName(LogPathTextBox.Text.Trim())
                    : $"4term-{DateTime.Now:yyyyMMdd}.log"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                LogPathTextBox.Text = dialog.FileName;
                LogCheckBox.Checked = true;
            }
        }

        /* True if the dialog was closed with Highlights, to open the
           highlight editor.  */
        public bool HighlightsRequested { get; private set; }

        /* Save the settings, close the dialog and ask for the highlight
           editor.  */
        private void HighlightsButton_Click(object sender, EventArgs e)
        {
            HighlightsRequested = true;
            SaveSettingsButton_Click(sender, e);
        }

        /* Let the user choose the font, and show it in the preview label.  */
        private void FontButton_Click(object sender, EventArgs e)
        {
            using (FontDialog fontDialog = new FontDialog())
            {
                fontDialog.Font = CreatePreviewFont();
                fontDialog.FontMustExist = true;
                fontDialog.MinSize = 6;
                fontDialog.MaxSize = 18;
                fontDialog.ShowEffects = true;

                if (fontDialog.ShowDialog() == DialogResult.OK)
                {
                    FontName = fontDialog.Font.Name;
                    FontSize = fontDialog.Font.SizeInPoints;

                    /* Keep both Bold and Italic ("Bold, Italic" parses back as
                       flags).  */
                    FontStyleName = (fontDialog.Font.Style & (FontStyle.Bold | FontStyle.Italic)).ToString();

                    FontStrikeout = fontDialog.Font.Strikeout;
                    FontUnderline = fontDialog.Font.Underline;

                    ExampleLabel.Font = CreatePreviewFont();
                }
            }
        }

        /* Let the user choose the background color.  */
        private void BackgroundColorPictureBox_Click(object sender, EventArgs e)
        {
            ChangeColor(ref BackColorValue);
            BackgroundColorPictureBox.BackColor = BackColorValue;
        }

        /* Let the user choose the color of sent data.  */
        private void TransmitColorButtonPictureBox_Click(object sender, EventArgs e)
        {
            ChangeColor(ref TransmitColorValue);
            TransmitColorPictureBox.BackColor = TransmitColorValue;
        }

        /* Let the user choose the color of received data.  */
        private void ReceiveColorButtonPictureBox_Click(object sender, EventArgs e)
        {
            ChangeColor(ref ReceiveColorValue);
            ReceiveColorPictureBox.BackColor = ReceiveColorValue;
        }

        /* Show the current settings in the dialog.  */
        private void LoadSettings()
        {
            /* Program.  */
            ShowWelcomeMessageCheckBox.Checked = Settings.Program.ShowWelcomeMessage;

            /* Form.  */
            FormWidthTextBox.Text = Settings.Window.Width.ToString();
            FormHeightTextBox.Text = Settings.Window.Height.ToString();

            /* RichTextBox.  */
            WordWrapCheckBox.Checked = Settings.Output.WordWrap;
            ToggleScrollingCheckBox.Checked = Settings.Output.Scroll;
            HexOutputCheckBox.Checked = Settings.Output.HexOutput;
            LocalEchoCheckBox.Checked = Settings.Output.LocalEcho;
            TimestampsCheckBox.Checked = Settings.Output.Timestamps;

            /* Log.  */
            LogCheckBox.Checked = Settings.Log.Enabled;
            LogPathTextBox.Text = Settings.Log.Path;

            /* Color.  */
            BackgroundColorPictureBox.BackColor = TupleToColor(Settings.Colors.Back);
            TransmitColorPictureBox.BackColor = TupleToColor(Settings.Colors.Transmit);
            ReceiveColorPictureBox.BackColor = TupleToColor(Settings.Colors.Receive);

            /* Font preview.  */
            ExampleLabel.Font = CreatePreviewFont();
        }

        /* Return the color with the red, green and blue values in COLOR.  */
        private static Color TupleToColor((int R, int G, int B) color)
        {
            return Color.FromArgb(color.R, color.G, color.B);
        }

        /* Show a color picker starting at COLOR, and store the chosen color in
           COLOR unless the user cancels.  */
        private static void ChangeColor(ref Color color)
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = color;
                if (dialog.ShowDialog() == DialogResult.OK)
                    color = Color.FromArgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
            }
        }

        /* Return the font style being edited, with underline and strikeout
           added.  */
        private FontStyle BuildFontStyle()
        {
            FontStyle style = Enum.TryParse(FontStyleName, out FontStyle parsed)
            ? parsed
            : FontStyle.Regular;

            if (FontUnderline)
                style |= FontStyle.Underline;

            if (FontStrikeout)
                style |= FontStyle.Strikeout;

            return style;
        }

        /* Return the font being edited.  An unknown family falls back to the
           system font, and a style the family lacks falls back to Regular.  */
        private Font CreatePreviewFont()
        {
            try
            {
                return new Font(FontName, FontSize, BuildFontStyle(), GraphicsUnit.Point);
            }
            catch (ArgumentException)
            {
                return new Font(FontName, FontSize, FontStyle.Regular, GraphicsUnit.Point);
            }
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
