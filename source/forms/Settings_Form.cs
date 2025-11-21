using System;
using System.Drawing;
using System.Windows.Forms;

namespace _4Term
{
    public partial class Settings_Form : Form
    {
        /*----------------------------------------------------------
         * Class Functions
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
        * Constructor
        *
        * Initializes the form and populates all UI controls
        * with current application settings.
        *---------------------------------------------------------*/
        public Settings_Form()
        {
            InitializeComponent();
            LoadSettings();
        }

        /*----------------------------------------------------------
        * SaveSettingsButton_Click
        *
        * Collects user input from the form and writes updated
        * values back to the Settings structure before persisting
        * them to disk.
        *---------------------------------------------------------*/
        private void SaveSettingsButton_Click(object sender, EventArgs e)
        {
            /* Program */
            Settings.Program.ShowWelcomeMessage = ShowWelcomeMessageCheckBox.Checked;

            /* Form */
            if (int.TryParse(FormWidthTextBox.Text, out int width))
                Settings.Form.Width = width;
            if (int.TryParse(FormHeightTextBox.Text, out int height))
                Settings.Form.Height = height;
            Settings.Form.AdvancedMode = AdvancedModeCheckBox.Checked;

            /* RichTextBox */
            Settings.RichTextBox.WordWrap = WordWrapCheckBox.Checked;
            Settings.RichTextBox.Scroll = ToggleScrollingCheckBox.Checked;
            Settings.RichTextBox.HexOutput = HexOutputCheckBox.Checked;
            Settings.RichTextBox.LocalEcho = LocalEchoCheckBox.Checked;

            /* Save changes */
            Settings.WriteXml();

            /* Close form */
            Close();
        }

        /*----------------------------------------------------------
        * FontButton_Click
        *
        * Displays a font selection dialog and applies the chosen
        * font configuration to both the preview label and the
        * stored RichTextBox settings.
        *---------------------------------------------------------*/
        private void FontButton_Click(object sender, EventArgs e)
        {
            using (FontDialog fontDialog = new FontDialog())
            {
                fontDialog.Font = new Font(
                new FontFamily(Settings.RichTextBox.Font),
                Settings.RichTextBox.Size,
                BuildFontStyle(),
                GraphicsUnit.Point);


                fontDialog.FontMustExist = true;
                fontDialog.MinSize = 6;
                fontDialog.MaxSize = 18;
                fontDialog.ShowEffects = true;


                if (fontDialog.ShowDialog() == DialogResult.OK)
                {
                    Settings.RichTextBox.Font = fontDialog.Font.Name;
                    Settings.RichTextBox.Size = (int)fontDialog.Font.Size;


                    FontStyle fs = fontDialog.Font.Style;


                    if (fs.HasFlag(FontStyle.Bold))
                        Settings.RichTextBox.FontStyle = "Bold";
                    else if (fs.HasFlag(FontStyle.Italic))
                        Settings.RichTextBox.FontStyle = "Italic";
                    else
                        Settings.RichTextBox.FontStyle = "Regular";


                    Settings.RichTextBox.Strikeout = fontDialog.Font.Strikeout;
                    Settings.RichTextBox.Underline = fontDialog.Font.Underline;


                    ExampleLabel.Font = new Font(
                    Settings.RichTextBox.Font,
                    Settings.RichTextBox.Size,
                    BuildFontStyle()
                    );
                }
            }
        }

        /*----------------------------------------------------------
        * BackgroundColorPictureBox_Click
        *
        * Opens color selector for background color and updates
        * both preview and stored value.
        *---------------------------------------------------------*/
        private void BackgroundColorPictureBox_Click(object sender, EventArgs e)
        {
            ChangeColor(ref Settings.Color.Back);
            BackgroundColorPictureBox.BackColor = TupleToColor(Settings.Color.Back);
        }

        /*----------------------------------------------------------
        * TransmitColorButtonPictureBox_Click
        *
        * Allows user to define transmit text color.
        *---------------------------------------------------------*/
        private void TransmitColorButtonPictureBox_Click(object sender, EventArgs e)
        {
            ChangeColor(ref Settings.Color.Transmit);
            TransmitColorPictureBox.BackColor = TupleToColor(Settings.Color.Transmit);
        }

        /*----------------------------------------------------------
        * ReceiveColorButtonPictureBox_Click
        *
        * Allows user to define receive text color.
        *---------------------------------------------------------*/
        private void ReceiveColorButtonPictureBox_Click(object sender, EventArgs e)
        {
            ChangeColor(ref Settings.Color.Receive);
            ReceiveColorPictureBox.BackColor = TupleToColor(Settings.Color.Receive);
        }

        /*----------------------------------------------------------
        * LoadSettings
        *
        * Populates all form controls with values from the
        * Settings structure during form initialization.
        *---------------------------------------------------------*/
        private void LoadSettings()
        {
            /* Program */
            ShowWelcomeMessageCheckBox.Checked = Settings.Program.ShowWelcomeMessage;

            /* Form */
            FormWidthTextBox.Text = Settings.Form.Width.ToString();
            FormHeightTextBox.Text = Settings.Form.Height.ToString();
            AdvancedModeCheckBox.Checked = Settings.Form.AdvancedMode;

            /* RichTextBox */
            WordWrapCheckBox.Checked = Settings.RichTextBox.WordWrap;
            ToggleScrollingCheckBox.Checked = Settings.RichTextBox.Scroll;
            HexOutputCheckBox.Checked = Settings.RichTextBox.HexOutput;
            LocalEchoCheckBox.Checked = Settings.RichTextBox.LocalEcho;

            /* Color */
            BackgroundColorPictureBox.BackColor = TupleToColor(Settings.Color.Back);
            TransmitColorPictureBox.BackColor = TupleToColor(Settings.Color.Transmit);
            ReceiveColorPictureBox.BackColor = TupleToColor(Settings.Color.Receive);


            ExampleLabel.Font = new Font(
            Settings.RichTextBox.Font,
            Settings.RichTextBox.Size,
            BuildFontStyle()
            );
        }

        /*----------------------------------------------------------
        * TupleToColor
        *
        * Converts an (R,G,B) tuple into a System.Drawing.Color.
        *---------------------------------------------------------*/
        private static Color TupleToColor((int R, int G, int B) color)
        {
            return Color.FromArgb(color.R, color.G, color.B);
        }

        /*----------------------------------------------------------
        * ChangeColor
        *
        * Opens color picker dialog using current color
        * and updates the referenced tuple upon confirmation.
        *---------------------------------------------------------*/
        private static void ChangeColor(ref (int R, int G, int B) colorTuple)
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = Color.FromArgb(colorTuple.R, colorTuple.G, colorTuple.B);
                if (dialog.ShowDialog() == DialogResult.OK)
                    colorTuple = (dialog.Color.R, dialog.Color.G, dialog.Color.B);
            }
        }

        /*----------------------------------------------------------
        * BuildFontStyle
        *
        * Constructs a FontStyle object based on stored
        * RichTextBox configuration values.
        *---------------------------------------------------------*/
        private FontStyle BuildFontStyle()
        {
            FontStyle style = Enum.TryParse(Settings.RichTextBox.FontStyle, out FontStyle parsed)
            ? parsed
            : FontStyle.Regular;


            if (Settings.RichTextBox.Underline)
                style |= FontStyle.Underline;


            if (Settings.RichTextBox.Strikeout)
                style |= FontStyle.Strikeout;


            return style;
        }

        /*----------------------------------------------------------
         * WndProc
         * 
         * Blocks right-clicks on title bar/borders.
        *---------------------------------------------------------*/
        protected override void WndProc(ref Message buffer)
        {
            if (buffer.Msg != 0xA3)
                base.WndProc(ref buffer);
        }
    }
}
