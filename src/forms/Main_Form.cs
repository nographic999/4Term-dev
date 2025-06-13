using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace _4Term
{
    public partial class Main_Form : Form
    {
        /*----------------------------------------------------------
         * Class variables
         * -------------------------------------------------------*/
        
        internal delegate void StringDelegate(string data);
        private readonly Serial Port = Serial.Instance;
        private readonly Timer AutoConnectTimer = new Timer();

        /* Tracks the current page group index (0-31) where each group contains 8 pages */
        private int PageIndex = 0;
        /* Tracks the current macro bank offset (0-511) where each bank contains 128 macros */
        private int MacroBank = 0;
        /* Tracks the base offset (0-112) of the currently selected 16-macro radio button group */
        private int CurrentRadioValue = 0;
        /* */
        private bool MacroEditingFlag = false;
        /* Path of the currently opened file */
        private string FilePath = null;

        private readonly Dictionary<string, int> radioButtonMapping = new Dictionary<string, int>
        {
            { "RadioButton0", 0 },
            { "RadioButton1", 16 },
            { "RadioButton2", 32 },
            { "RadioButton3", 48 },
            { "RadioButton4", 64 },
            { "RadioButton5", 80 },
            { "RadioButton6", 96 },
            { "RadioButton7", 112 }

        };

        /*----------------------------------------------------------
         * Form
         * ---------------------------------------------------------
         * DEVELOPMENT STATUS
         *
         * [✓] Main_Form(constructor)
         * [✓] Main_Form_Load
         * [~] Main_Form_Shown - needs connect on start optimization
         * [~] Main_Form_FormClosing - needs closing optimization
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * Main_Form
         * 
         * Main application form constructor.
         * Initializes UI components and sets up core event handlers.
         *---------------------------------------------------------*/
        public Main_Form()
        {
            /* Initialize all UI components defined in the designer */
            InitializeComponent();

            /* Set up event subscriptions (lightweight) */
            Port.StatusChanged += ChangeStatus;
            Port.DataReceived += DataReceive;
        }

        /*----------------------------------------------------------
         * Main_Form_Load
         * 
         * Runs when form is initialized but not yet visible
         * Use for: Settings, data loading
         *---------------------------------------------------------*/
        private void Main_Form_Load(object sender, EventArgs e)
        {
            /* Configuration file handling */
            if (!File.Exists(Settings.XmlName))
            {
                DialogResult result = MessageBox.Show("The configuration file does not exist. Would you like to create a new configuration file?",
                                                   "Create New Configuration",
                                                   MessageBoxButtons.YesNo,
                                                   MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                    Settings.WriteXml();
                else
                    Environment.Exit(0);
            }

            /* Load settings and configure UI */
            Settings.ReadXml();
            Size = new Size(Settings.Form.Width, Settings.Form.Height);

            /* Initialize font settings */
            string buffer = "";
            RichTextBox.Font = cFont.ParseFontString(
                Settings.RichTextBox.Font + "-" +
                Settings.RichTextBox.Size + "-" +
                Settings.RichTextBox.FontStyle,
                Settings.RichTextBox.Underline,
                Settings.RichTextBox.Strikeout,
                ref buffer);

            /* Set UI control states */
            WordWrapToolStripMenuItem.Checked = Settings.RichTextBox.WordWrap;
            ToggleScrollingToolStripMenuItem.Checked = Settings.RichTextBox.Scroll;

            /* Initialize UI components */
            AssignButtonClickHandlers();
            LoadGroupContent();
        }

        /*----------------------------------------------------------
         * Main_Form_Shown
         * 
         * Runs when form is completely visible to user
         * Use for: focus, animations, background tasks
         *---------------------------------------------------------*/
        private void Main_Form_Shown(object sender, EventArgs e)
        {
            /* Start background operations */
            AutoConnectTimer.Interval = 100;
            AutoConnectTimer.Tick += ReconnectTick;

            /* UI-dependent initialization */
            WelcomeMsg();
        }

        private void Main_Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Port.IsOpen)
                Port.Close();
            base.OnClosed(e);
        }

        /*----------------------------------------------------------
         * Buttons
         * ---------------------------------------------------------
         * DEVELOPMENT STATUS
         *
         * [ ] ProgButton_Click - piece of code disabled, needs optimization and testing
         * [~] SendButton_Click - needs optimization and testing
         * [✓] ClearButton_Click
         * [✓] OptionsButton_Click
         * [✓] AutoConnectButton_Click
         * [✓] ConnectButton_Click
         * [✓] SwapButton_Click
         * [✓] RadioButton_Click - needs optimization and testing
         * [~] MacroButton_Click - piece of code disabled, needs optimization and testing 
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * ProgButton_Click
         * 
         * Handles macro button editing confirmation
         * Saves macro settings when text fields are valid
         *---------------------------------------------------------*/
        private void ProgButton_Click(object sender, EventArgs e)
        {
            /* Validate that the command input box */
            if (string.IsNullOrWhiteSpace(MacroBox.Text))
    {
                MessageBox.Show("Command text cannot be empty", "Validation Error",
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            /* Validate that the button text input box */
            if (string.IsNullOrWhiteSpace(MacroTextBox.Text))
            {
                MessageBox.Show("Button text cannot be empty", "Validation Error",
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            /* Validate that the tab text input box */
            if (string.IsNullOrWhiteSpace(TabBox.Text))
            {
                MessageBox.Show("Tab text cannot be empty", "Validation Error",
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            /* Store trimmed command text in settings array */
            //Settings.MacroCommand.Text[MacroButton + ButtonNumber + MacroBank] = MacroBox.Text.Trim();

            /* Store trimmed button display text in settings array */
            //Settings.MacroText.Text[MacroButton + ButtonNumber + MacroBank] = MacroTextBox.Text.Trim();

            /* Store trimmed tab/page title in settings array */
            //Settings.Page.Text[MacroPage + PageIndex] = TabBox.Text.Trim();
            
            /* Save changes to configuration file */
            Settings.WriteXml();

            /* Refresh the UI with new button set */
            LoadGroupContent();
        }

        /*----------------------------------------------------------
         * SendButton_Click
         * 
         * Handles data sending via serial port
         * Shows data in RichTextBox
         *---------------------------------------------------------*/
        private void SendButton_Click(object sender, EventArgs e)
        {
            /* Capture current input text */
            string buffer = ComboBox.Text ?? string.Empty;

            /* Add to command history (avoid empty strings and duplicates) */
            if (!string.IsNullOrEmpty(buffer) && !ComboBox.Items.Contains(buffer))
            {
                ComboBox.Items.Add(buffer);
            }

            /* Maintain input focus for next command */
            ComboBox.Focus();

            if (!string.IsNullOrEmpty(buffer))
            {
                /* Send and get the actual transmission (with line endings) */
                string message = Port.Send(buffer);

                if (Settings.RichTextBox.LocalEcho)
                {
                    /* Show exactly what was sent (including line endings) */
                    AddLine(message, 0);
                }
            }
        }

        /*----------------------------------------------------------
         * ClearButton_Click
         * 
         * Clears the RichTextBox content
         *---------------------------------------------------------*/
        private void ClearButton_Click(object sender, EventArgs e)
        {
            RichTextBox.Clear();
        }

        /*----------------------------------------------------------
         * OptionsButton_Click
         * 
         * Opens the Serial Port Options configuration dialog
         *---------------------------------------------------------*/
        private void OptionsButton_Click(object sender, EventArgs e)
        {
            SerialPortOptionsForm OptionsForm = new SerialPortOptionsForm();
            OptionsForm.ShowDialog();
        }

        /*----------------------------------------------------------
         * AutoConnectButton_Click
         * 
         * Toggles automatic connection timer on/off
        *---------------------------------------------------------*/
        private void AutoConnectButton_Click(object sender, EventArgs e)
        {
            AutoConnectTimer.Enabled = !AutoConnectTimer.Enabled;
            AutoConnectButton.ForeColor = AutoConnectTimer.Enabled ? Color.Red : Color.Black;
        }

        /*----------------------------------------------------------
         * ConnectButton_Click
         * 
         * Manages serial port connection state
        *---------------------------------------------------------*/
        private void ConnectButton_Click(object sender, EventArgs e)
        {
            if (Port.IsOpen) Port.Close(); else Port.Open();
            ConnectButton.Text = Port.IsOpen ? "Disconnect" : "Connect";
            ConnectButton.ForeColor = Port.IsOpen ? Color.Red : Color.Green;
        }

        /*----------------------------------------------------------
         * SwapButton_Click
         * 
         * Handles circular navigation through button/page groups
         *---------------------------------------------------------*/

        private void SwapButton_Click(object sender, EventArgs e)
        {
            /* Circular page navigation (0-31 pages in 8-page groups) */
            PageIndex = (PageIndex + 8) % 32;

            /* Circular button bank navigation (0-511 buttons in 128-button groups) */
            MacroBank = (MacroBank + 128) % 512;

            /* Refresh the UI with new button set */
            LoadGroupContent();
        }

        /*----------------------------------------------------------
         * RadioButton_Click
         * 
         * Handles radio button selection for macro button navigation
         *---------------------------------------------------------*/

        private void RadioButton_Click(object sender, EventArgs e)
        {
            /* Safely cast sender to RadioButton and get mapped value */
            if (sender is RadioButton selectedRadioButton && radioButtonMapping.TryGetValue(selectedRadioButton.Name, out int radioValue))
            {
                CurrentRadioValue = radioValue;

                LoadGroupContent();
            }
        }

        /*----------------------------------------------------------
         * MacroButton_Click
         * 
         * NEEDS COMMENT
         *---------------------------------------------------------*/
        public async void MacroButton_Click(int macroIndex)
        {
            if (!MacroEditingFlag)
            {
                string command = Settings.MacroCommand.Text[macroIndex + CurrentRadioValue + MacroBank];
                string[] commands = command.Split(new[] { "$0D" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string cmd in commands)
                {
                    if (!string.IsNullOrEmpty(cmd))
                    {
                        string lineEndedWith = "";
                        lineEndedWith = Port.Send(cmd);
                        if (Settings.RichTextBox.LocalEcho && lineEndedWith != "0")
                        {
                            AddLine(cmd + lineEndedWith, 0);
                        }
                        else
                        {
                            AddLine(cmd + lineEndedWith, 0);
                            //AddLine("Port is closed!\r", 0);
                        }

                        await Task.Delay(50);
                    }
                }
            }
            else
            {
                //MacroBox.Text = Settings.MacroCommand.Text[macroIndex + CurrentRadioValue + MacroBank];
                //MacroTextBox.Text = Settings.MacroText.Text[macroIndex + CurrentRadioValue + MacroBank];
                //TabBox.Text = Settings.Page.Text[MacroPage + PageIndex];
                //MacroButton = macroIndex;
            }
        }

        /*----------------------------------------------------------
         * ComboBox
         * ---------------------------------------------------------
         * DEVELOPMENT STATUS
         *
         * [✓] ComboBox_Enter
         * [✓] ComboBox_KeyDown
         * [✓] ComboBox_Leave
         *---------------------------------------------------------*/

        private void ComboBox_Enter(object sender, EventArgs e) // nothing changed
        {
            PasteToolStripMenuItem.Enabled = CopyToolStripMenuItem.Enabled = CutToolStripMenuItem.Enabled = false;
        }

        private void ComboBox_KeyDown(object sender, KeyEventArgs keyboard)
        {
            if (keyboard.KeyCode == Keys.Enter)
            {
                SendButton_Click(sender, keyboard);
                keyboard.Handled = true;
                keyboard.SuppressKeyPress = true;
            }
        }

        private void ComboBox_Leave(object sender, EventArgs e) // nothing changed
        {
            PasteToolStripMenuItem.Enabled = CopyToolStripMenuItem.Enabled = CutToolStripMenuItem.Enabled = true;
        }

        /*----------------------------------------------------------
         * Events
         * ---------------------------------------------------------
         * DEVELOPMENT STATUS
         *
         * [✓] ChangeStatus
         * [✓] DataReceive
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * ChangeStatus
         * 
         * Thread-safe UI updater for connection status changes.
         *---------------------------------------------------------*/
        public void ChangeStatus(string buffer)
        {
            if (InvokeRequired)
            {
                Invoke(new StringDelegate(ChangeStatus), new object[] { buffer });
                return;
            }
            ConnectButton.Text = Port.IsOpen ? "Disconnect" : "Connect";
            ConnectButton.ForeColor = Port.IsOpen ? Color.Red : Color.Green;
            Text = buffer;
        }

        /*----------------------------------------------------------
         * DataReceive
         * 
         * Thread-safe method for processing incoming serial data.
         *---------------------------------------------------------*/
        public void DataReceive(string dataIn)
        {
            if (InvokeRequired)
            {
                Invoke(new StringDelegate(DataReceive), new object[] { dataIn });
                return;
            }
            AddLine(dataIn, 1);
        }

        /*----------------------------------------------------------
         * Timers
         * ---------------------------------------------------------
         * DEVELOPMENT STATUS
         *
         * [✓] ReconnectTick
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * ReconnectTick
         * 
         * Timer-triggered port reconnection handler.
         *---------------------------------------------------------*/
        private void ReconnectTick(object sender, EventArgs e)
        {
            ConnectButton.Text = Port.IsOpen ? "Disconnect" : "Connect";
            ConnectButton.ForeColor = Port.IsOpen ? Color.Red : Color.Green;
            if (!Port.IsOpen) Port.Open();
        }

        /*----------------------------------------------------------
         * Helper Functions
         * ---------------------------------------------------------
         * DEVELOPMENT STATUS
         * 
         * [✓] AssignButtonClickHandlers
         * [✓] LoadGroupContent
         * [~] AddLine - needs new code
         * [✓] WelcomeMsg
         * [✓] WndProc
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
        * AssignButtonClickHandlers
        * 
        * Attaches click event handlers to MacroButton controls (0-15).
        *---------------------------------------------------------*/
        private void AssignButtonClickHandlers()
        {
            for (int i = 0; i <= 15; i++)
            {
                /* Find the button in the form */
                Button macroButton = (Button)this.Controls.Find($"MacroButton{i}", true).FirstOrDefault();
                if (macroButton != null)
                {
                    /* Store current button number to avoid closure issue */
                    int buffer = i;

                    /* Set click handler for this button */
                    macroButton.Click += (sender, e) => MacroButton_Click(buffer);
                }
            }
        }

        /*----------------------------------------------------------
        * LoadGroupContent
        * 
        * Loads text labels for RadioButtons (0-7) and MacroButtons (0-15)
        *---------------------------------------------------------*/
        private void LoadGroupContent()
        {
            /* Update RadioButton texts (0-7) */
            for (int i = 0; i <= 7; i++)
            {
                /* Find RadioButton control by name (searches child controls recursively) */
                RadioButton radioButton = (RadioButton)this.Controls.Find($"RadioButton{i}", true).FirstOrDefault();

                /* Apply text from settings if the control exists */
                if (radioButton != null)
                    radioButton.Text = Settings.Page.Text[i + PageIndex];
            }

            /* Update MacroButton texts (0-15) */
            for (int i = 0; i <= 15; i++)
            {
                /* Find MacroButton control by name (searches child controls recursively) */
                Button macroButton = (Button)this.Controls.Find($"MacroButton{i}", true).FirstOrDefault();

                /* Apply text from settings with MacroBank and CurrentRadioValue offsets */
                if (macroButton != null)
                    macroButton.Text = Settings.MacroText.Text[i + MacroBank + CurrentRadioValue];
            }
        }

        /*----------------------------------------------------------
        * AddLine
        * 
        * Adds a line of text to the RichTextBox with configurable formatting.
        * *---------------------------------------------------------*/
        public void AddLine(string line, int type)
        {
            if (type == 0)
                RichTextBox.SelectionColor = Color.FromArgb(Settings.TransmitColor.R, Settings.TransmitColor.G, Settings.TransmitColor.B);
            else
                RichTextBox.SelectionColor = Color.FromArgb(Settings.ReceiveColor.R, Settings.ReceiveColor.G, Settings.ReceiveColor.B);
            string buffer = "";
            if (Settings.RichTextBox.HexOutput)
            {
                byte[] lineBytes = Encoding.UTF8.GetBytes(line + buffer);
                string lineAsString = BitConverter.ToString(lineBytes).Replace("-", " ");
                RichTextBox.AppendText($" [{lineAsString}]");
            }
            else
                RichTextBox.AppendText(line + buffer);
            if (Settings.RichTextBox.Scroll)
            {
                RichTextBox.SelectionStart = RichTextBox.Text.Length;
                RichTextBox.ScrollToCaret();
            }
        }

        /*----------------------------------------------------------
        * WelcomeMsg
        * 
        * Displays an animated ASCII art welcome message in a RichTextBox.
        * *---------------------------------------------------------*/
        public async void WelcomeMsg()
        {
            RichTextBox.BackColor = Color.Black;
            string[] asciiLines = new string[]
            {
                "",
                "",
                "",
                "",
                "",
                "                                   Welcome to 4Term Terminal!",
                "  ____________/\\\\\\_____/\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\__________________________________________________",
                "   __________/\\\\\\\\\\____\\///////\\\\\\/////___________________________________________________",
                "    ________/\\\\\\/\\\\\\__________\\/\\\\\\________________________________________________________",
                "     ______/\\\\\\/\\/\\\\\\__________\\/\\\\\\___________/\\\\\\\\\\\\\\\\___/\\\\/\\\\\\\\\\\\\\_____/\\\\\\\\\\__/\\\\\\\\\\___",
                "      ____/\\\\\\/__\\/\\\\\\__________\\/\\\\\\_________/\\\\\\/////\\\\\\_\\/\\\\\\/////\\\\\\__/\\\\\\///\\\\\\\\\\///\\\\\\_",
                "       __/\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\_______\\/\\\\\\________/\\\\\\\\\\\\\\\\\\\\\\__\\/\\\\\\___\\///__\\/\\\\\\_\\//\\\\\\__\\/\\\\\\_",
                "        _\\///////////\\\\\\//________\\/\\\\\\_______\\//\\\\///////___\\/\\\\\\_________\\/\\\\\\__\\/\\\\\\__\\/\\\\\\_",
                "         ___________\\/\\\\\\__________\\/\\\\\\________\\//\\\\\\\\\\\\\\\\\\\\_\\/\\\\\\_________\\/\\\\\\__\\/\\\\\\__\\/\\\\\\_",
                "          ___________\\///___________\\///__________\\//////////__\\///__________\\///___\\///___\\///__",
                "                                                                           Quick & Dirty 0.0.1 x64"
            };
            // Colors to cycle through
            Color[] colors = new Color[]
            {
                Color.Firebrick,       // deep red
                Color.DarkOrange,      // rich orange
                Color.Gold,            // warm yellow
                Color.MediumSeaGreen,  // muted green
                Color.SteelBlue,       // soft blue-gray
                Color.MediumSlateBlue, // cool purple-blue
                Color.Tomato,          // bright red-orange
                Color.MediumTurquoise, // vibrant teal
                Color.SandyBrown,      // light brown-orange
                Color.MediumVioletRed  // purple-pink
            };
            RichTextBox.Clear();
            for (int i = 0; i < asciiLines.Length; i++)
            {
                RichTextBox.SelectionColor = colors[i % colors.Length];
                RichTextBox.AppendText(asciiLines[i] + "\r\n");
                await Task.Delay(100);
            }
            await Task.Delay(250);
            RichTextBox.Clear();
            RichTextBox.ForeColor = Color.FromArgb(Settings.ForeColor.R, Settings.ForeColor.G, Settings.ForeColor.B);
            RichTextBox.BackColor = Color.FromArgb(Settings.BackColor.R, Settings.BackColor.G, Settings.BackColor.B);
        }

        /*----------------------------------------------------------
         * WndProc
         * 
         * Blocks right-clicks on title bar/borders.
        *---------------------------------------------------------*/
        protected override void WndProc(ref Message buffer)
        {
            if (buffer.Msg == 0xA3)
            {
                return;
            }
            base.WndProc(ref buffer);
        }
    }
}
