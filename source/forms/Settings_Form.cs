using System;
using System.Drawing;
using System.Windows.Forms;

namespace _4Term
{
    public partial class Settings_Form : Form
    {
        /*----------------------------------------------------------
         * Class Functions
         * ---------------------------------------------------------
         * DEVELOPMENT STATUS
         *
         * [✓] Settings_Form (Constructor)
         * [✓] SaveSettingsButton_Click
         * [✓] FontButton_Click
         * [✓] BackgroundColorButton_Click
         * [✓] TransmitColorButton_Click
         * [✓] ReceiveColorButton_Click
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * SerialPortOptionsForm (Constructor)
         * 
         * Initializes serial port configuration form components.
         *---------------------------------------------------------*/
        public Settings_Form()
        {
            InitializeComponent();

            /* Program */
            CheckForUpdatesCheckBox.Checked = Settings.Program.CheckForUpdates;
            ShowWelcomeMessageCheckBox.Checked = Settings.Program.ShowWelcomeMessage;

            /* Form */
            FormWidthTextBox.Text = Settings.Form.Width.ToString();
            FormHeightTextBox.Text = Settings.Form.Height.ToString();
            AdvancedModeCheckBox.Checked = Settings.Form.AdvancedMode;
            ExternalToolsCheckBox.Checked = Settings.Form.ExternalTools;

            /* RichTextBox */
            WordWrapCheckBox.Checked = Settings.RichTextBox.WordWrap;
            ToggleScrollingCheckBox.Checked = Settings.RichTextBox.Scroll;
            HexOutputCheckBox.Checked = Settings.RichTextBox.HexOutput;
            LocalEchoCheckBox.Checked = Settings.RichTextBox.LocalEcho;

            /* External Tools*/
            ExternalToolsEnableCheckBox0.Checked = Settings.ExternalTools.Enabled[0];
            ExternalToolsEnableCheckBox1.Checked = Settings.ExternalTools.Enabled[1];
            ExternalToolsEnableCheckBox2.Checked = Settings.ExternalTools.Enabled[2];
            ExternalToolsEnableCheckBox3.Checked = Settings.ExternalTools.Enabled[3];
            ExternalToolsEnableCheckBox4.Checked = Settings.ExternalTools.Enabled[4];
            ExternalToolsEnableCheckBox5.Checked = Settings.ExternalTools.Enabled[5];
            ExternalToolsEnableCheckBox6.Checked = Settings.ExternalTools.Enabled[6];
            ExternalToolsEnableCheckBox7.Checked = Settings.ExternalTools.Enabled[7];

            ExternalToolsTextTextBox0.Text = Settings.ExternalTools.Text[0];
            ExternalToolsTextTextBox1.Text = Settings.ExternalTools.Text[1];
            ExternalToolsTextTextBox2.Text = Settings.ExternalTools.Text[2];
            ExternalToolsTextTextBox3.Text = Settings.ExternalTools.Text[3];
            ExternalToolsTextTextBox4.Text = Settings.ExternalTools.Text[4];
            ExternalToolsTextTextBox5.Text = Settings.ExternalTools.Text[5];
            ExternalToolsTextTextBox6.Text = Settings.ExternalTools.Text[6];
            ExternalToolsTextTextBox7.Text = Settings.ExternalTools.Text[7];

            ExternalToolsAppTextBox0.Text = Settings.ExternalTools.Tool[0];
            ExternalToolsAppTextBox1.Text = Settings.ExternalTools.Tool[1];
            ExternalToolsAppTextBox2.Text = Settings.ExternalTools.Tool[2];
            ExternalToolsAppTextBox3.Text = Settings.ExternalTools.Tool[3];
            ExternalToolsAppTextBox4.Text = Settings.ExternalTools.Tool[4];
            ExternalToolsAppTextBox5.Text = Settings.ExternalTools.Tool[5];
            ExternalToolsAppTextBox6.Text = Settings.ExternalTools.Tool[6];
            ExternalToolsAppTextBox7.Text = Settings.ExternalTools.Tool[7];
        }

        /*----------------------------------------------------------
         * SaveSettingsButton_Click
         * 
         * Handles the Save button click in the settings form.
         *---------------------------------------------------------*/
        private void SaveSettingsButton_Click(object sender, EventArgs e)
        {
            /* Program */
            Settings.Program.CheckForUpdates = CheckForUpdatesCheckBox.Checked;
            Settings.Program.ShowWelcomeMessage = ShowWelcomeMessageCheckBox.Checked;

            /* Form */
            Settings.Form.Width = int.Parse(FormWidthTextBox.Text);
            Settings.Form.Height = int.Parse(FormHeightTextBox.Text);
            Settings.Form.AdvancedMode = AdvancedModeCheckBox.Checked;
            Settings.Form.ExternalTools = ExternalToolsCheckBox.Checked;

            /* RichTextBox */
            Settings.RichTextBox.WordWrap = WordWrapCheckBox.Checked;
            Settings.RichTextBox.Scroll = ToggleScrollingCheckBox.Checked;
            Settings.RichTextBox.HexOutput = HexOutputCheckBox.Checked;
            Settings.RichTextBox.LocalEcho = LocalEchoCheckBox.Checked;

            /* External Tools*/
            Settings.ExternalTools.Enabled[0] = ExternalToolsEnableCheckBox0.Checked;
            Settings.ExternalTools.Enabled[1] = ExternalToolsEnableCheckBox1.Checked;
            Settings.ExternalTools.Enabled[2] = ExternalToolsEnableCheckBox2.Checked;
            Settings.ExternalTools.Enabled[3] = ExternalToolsEnableCheckBox3.Checked;
            Settings.ExternalTools.Enabled[4] = ExternalToolsEnableCheckBox4.Checked;
            Settings.ExternalTools.Enabled[5] = ExternalToolsEnableCheckBox5.Checked;
            Settings.ExternalTools.Enabled[6] = ExternalToolsEnableCheckBox6.Checked;
            Settings.ExternalTools.Enabled[7] = ExternalToolsEnableCheckBox7.Checked;

            Settings.ExternalTools.Text[0] = ExternalToolsTextTextBox0.Text;
            Settings.ExternalTools.Text[1] = ExternalToolsTextTextBox1.Text;
            Settings.ExternalTools.Text[2] = ExternalToolsTextTextBox2.Text;
            Settings.ExternalTools.Text[3] = ExternalToolsTextTextBox3.Text;
            Settings.ExternalTools.Text[4] = ExternalToolsTextTextBox4.Text;
            Settings.ExternalTools.Text[5] = ExternalToolsTextTextBox5.Text;
            Settings.ExternalTools.Text[6] = ExternalToolsTextTextBox6.Text;
            Settings.ExternalTools.Text[7] = ExternalToolsTextTextBox7.Text;

            Settings.ExternalTools.Tool[0] = ExternalToolsAppTextBox0.Text;
            Settings.ExternalTools.Tool[1] = ExternalToolsAppTextBox1.Text;
            Settings.ExternalTools.Tool[2] = ExternalToolsAppTextBox2.Text;
            Settings.ExternalTools.Tool[3] = ExternalToolsAppTextBox3.Text;
            Settings.ExternalTools.Tool[4] = ExternalToolsAppTextBox4.Text;
            Settings.ExternalTools.Tool[5] = ExternalToolsAppTextBox5.Text;
            Settings.ExternalTools.Tool[6] = ExternalToolsAppTextBox6.Text;
            Settings.ExternalTools.Tool[7] = ExternalToolsAppTextBox7.Text;

            /* Save changes */
            Settings.WriteXml();

            /* Close form */
            Close();
        }

        /*----------------------------------------------------------
         * FontButton_Click
         * 
         * Opens a font dialog and updates RichTextBox font settings.
         *---------------------------------------------------------*/
        private void FontButton_Click(object sender, EventArgs e)
        {
            using (FontDialog fontDialog = new FontDialog())
            {
                FontStyle style = Enum.TryParse(Settings.RichTextBox.FontStyle, out FontStyle s) ? s : FontStyle.Regular;

                if (Settings.RichTextBox.Underline)
                    style |= FontStyle.Underline;  // add underline flag

                if (Settings.RichTextBox.Strikeout)
                    style |= FontStyle.Strikeout;  // add strikeout flag

                // Create the font with combined style flags
                fontDialog.Font = new Font(
                    new FontFamily(Settings.RichTextBox.Font),
                    Settings.RichTextBox.Size,
                    style,
                    GraphicsUnit.Point);

                fontDialog.FontMustExist = true;

                /* Show dialog and set settings if user confirms */
                if (fontDialog.ShowDialog() == DialogResult.OK)
                {
                    /* Set selected font settings */
                    Settings.RichTextBox.Font = fontDialog.Font.Name;
                    Settings.RichTextBox.Size = (int)fontDialog.Font.Size;

                    /* Simplified style name */
                    Settings.RichTextBox.FontStyle = fontDialog.Font.Style.ToString().Contains("Bold") ? "Bold"
                                                     : fontDialog.Font.Style.ToString().Contains("Italic") ? "Italic"
                                                     : "Regular";

                    Settings.RichTextBox.Strikeout = fontDialog.Font.Strikeout;
                    Settings.RichTextBox.Underline = fontDialog.Font.Underline;
                }
            }
        }

        /*----------------------------------------------------------
         * BackgroundColorButton_Click
         * 
         * Sets background color from color picker.
         *---------------------------------------------------------*/
        private void BackgroundColorButton_Click(object sender, EventArgs e) => ChangeColor(ref Settings.Color.Back);

        /*----------------------------------------------------------
         * TransmitColorButton_Click
         * 
         * Sets transmit color from color picker.
         *---------------------------------------------------------*/
        private void TransmitColorButton_Click(object sender, EventArgs e) => ChangeColor(ref Settings.Color.Transmit);

        /*----------------------------------------------------------
         * ReceiveColorButton_Click
         * 
         * Sets receive color from color picker.
         *---------------------------------------------------------*/
        private void ReceiveColorButton_Click(object sender, EventArgs e) => ChangeColor(ref Settings.Color.Receive);

        /*----------------------------------------------------------
         * Helper Functions
         * ---------------------------------------------------------
         * DEVELOPMENT STATUS
         * 
         * [✓] ChangeColor
         * [✓] WndProc
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * ChangeColor
         * 
         * Opens a color picker dialog preloaded with the current
         * RGB color tuple. If the user selects a new color and
         * confirms, updates the provided color tuple.
         *---------------------------------------------------------*/
        private void ChangeColor(ref (int R, int G, int B) colorTuple)
        {
            using (ColorDialog colorDialog = new ColorDialog())
            {
                colorDialog.Color = Color.FromArgb(colorTuple.R, colorTuple.G, colorTuple.B);
                if (colorDialog.ShowDialog() == DialogResult.OK)
                    colorTuple = (colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);
            }
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
