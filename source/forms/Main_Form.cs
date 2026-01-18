using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace _4Term
{
    public partial class Main_Form : Form
    {
        /*----------------------------------------------------------
         * Class variables
         * -------------------------------------------------------*/

        /* Delegate for thread-safe operations that involve passing a string */
        internal delegate void StringDelegate(string data);
        /* Singleton instance of the Serial communication handler */
        private readonly Serial Port = Serial.Instance;
        /* Tracks the current page group index (0-31) where each group contains 8 pages */
        private int PageIndex = 0;
        /* Tracks the current macro bank offset (0-511) where each bank contains 128 macros */
        private int MacroBank = 0;
        /* Stores the index of the currently selected radio button */
        private int CurrentRadioValue = 0;
        /* Stores the index of the currently selected macro button */
        private int CurrentButtonValue = 0;
        /* Flag indicating whether the application is currently in macro editing mode */
        private bool MacroEditingFlag = false;
        /* Path of the currently opened file */
        private string FilePath = null;

        /* Radio Button to Macro Offset Mapping */
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

        /* Current repeat counts for macros M1 through M5 (-1 indicates disabled) */
        private readonly int[] CurrentMRepeat = new int[5] { -1, -1, -1, -1, -1 };
        /* Flags indicating whether macros M1 through M5 are active */
        private readonly bool[] MFlag = new bool[] { false, false, false, false, false };
        /* Flags used to control flicker effect for macros M1 through M5 */
        private readonly bool[] MFlicker = new bool[] { false, false, false, false, false };

        /*----------------------------------------------------------
         * Timers
         * -------------------------------------------------------*/

        /* Timer used for periodically attempting automatic connection */
        private readonly Timer AutoConnectTimer = new Timer();
        /* Timers used for M1 to M5 macro repeat functionality */
        private readonly Timer[] MTimer = new Timer[5]
        {
            new Timer(),
            new Timer(),
            new Timer(),
            new Timer(),
            new Timer()
        };

        /*----------------------------------------------------------
         * Colors
         * -------------------------------------------------------*/

        Color BackgroundColor = Color.Empty;
        Color TransmitColor = Color.Empty;
        Color ReceiveColor = Color.Empty;

        /*----------------------------------------------------------
         * Class Functions
         * -------------------------------------------------------*/

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
            /* Load settings */
            Settings.ReadXml();

            /* Set Form size*/
            Size = new Size(Settings.Form.Width, Settings.Form.Height);

            /* Initialize color settings */
            BackgroundColor = Color.FromArgb(Settings.Color.Back.R, Settings.Color.Back.G, Settings.Color.Back.B);
            TransmitColor = Color.FromArgb(Settings.Color.Transmit.R, Settings.Color.Transmit.G, Settings.Color.Transmit.B);
            ReceiveColor = Color.FromArgb(Settings.Color.Receive.R, Settings.Color.Receive.G, Settings.Color.Receive.B);            

            /* Initialize font settings */
            FontStyle style = Enum.TryParse(Settings.RichTextBox.FontStyle, out FontStyle parsedStyle)
                ? parsedStyle
                : FontStyle.Regular;

            if (Settings.RichTextBox.Underline)
                style |= FontStyle.Underline;

            if (Settings.RichTextBox.Strikeout)
                style |= FontStyle.Strikeout;

            RichTextBox.Font = new Font(
                new FontFamily(Settings.RichTextBox.Font),
                Settings.RichTextBox.Size,
                style
            );

            /* Set UI control states */
            WordWrapToolStripMenuItem.Checked = Settings.RichTextBox.WordWrap;
            ToggleScrollingToolStripMenuItem.Checked = Settings.RichTextBox.Scroll;
            DTRPictureBox.BackColor = Settings.Port.Dtr ? Color.Green : Color.Transparent;
            RTSPictureBox.BackColor = Settings.Port.Rts ? Color.Green : Color.Transparent;
            SetAdvancedMode(Settings.Form.AdvancedMode);
            SwapButton.Visible = SwapButton.Enabled = Settings.Program.IsSwapButtonVisible;

            /* Initialize UI components */
            UpdateAppendButtons();
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

            /* Initialize each macro timer */
            for (int i = 0; i < MTimer.Length; i++)
            {
                MTimer[i] = new Timer
                {
                    Interval = 500,
                    Tag = i
                };
                MTimer[i].Tick += MGenericTick;
            }

            /* UI-dependent initialization */
            if (Settings.Program.FirstStart || Settings.Program.ShowWelcomeMessage)
                WelcomeMsg();
            else
            {
                RichTextBox.Clear();
                RichTextBox.BackColor = BackgroundColor;
                RichTextBox.ForeColor = TransmitColor;             
            }

            /* Reset FirstStart flag and save settings on first run */
            if (Settings.Program.FirstStart)
            {
                Settings.Program.FirstStart = false;
                Settings.WriteXml();
            }                  
        }

        /*----------------------------------------------------------
         * Main_Form_FormClosing
         * 
         * Handles cleanup tasks when the form is closing.
         * Ensures the serial port is closed and performs base cleanup.
         *---------------------------------------------------------*/
        private void Main_Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Port.IsOpen)
                Port.Close();
            base.OnClosed(e);
        }

        /*----------------------------------------------------------
         * CloseButton_Click
         * 
         * Handles toggling of macro editing mode when the Close button
         * is clicked. Updates the UI and internal flags to reflect
         * whether macros are in editing mode.
        *---------------------------------------------------------*/
        private void CloseButton_Click(object sender, EventArgs e)
        {
            MacroEditingFlag = !MacroEditingFlag;
            SetMacroSetMode(MacroEditingFlag);
        }

        /*----------------------------------------------------------
         * ProgButton_Click
         * 
         * Handles macro button editing confirmation
         * Saves macro settings when text fields are valid
         *---------------------------------------------------------*/
        private void AlienButton_Click(object sender, EventArgs e)
        {
            /* Validate that the tab text input box */
            if (string.IsNullOrWhiteSpace(PageBox.Text))
            {
                ErrorMessage("Tab cannot be empty\n");
                return;
            }            

            /* Validate that the button text input box */
            if (string.IsNullOrWhiteSpace(MacroTextBox.Text))
            {
                ErrorMessage("Text cannot be empty\n");
                return;
            }

            /* Validate that the command input box */
            if (string.IsNullOrWhiteSpace(MacroBox.Text))
            {
                ErrorMessage("Macro cannot be empty\n");
                return;
            }

            /* Store trimmed tab/page title in settings array */
            Settings.Page.Text[CurrentRadioValue / 16 + PageIndex] = PageBox.Text.Trim();
            InfoMessage($"Page{CurrentRadioValue / 16 + PageIndex} set to: " + PageBox.Text.Trim() + "\n");

            /* Store trimmed button display text in settings array */
            Settings.MacroText.Text[CurrentButtonValue + CurrentRadioValue + MacroBank] = MacroTextBox.Text.Trim();
            InfoMessage($"Text{CurrentButtonValue + CurrentRadioValue + MacroBank} set to: " + MacroTextBox.Text.Trim() + "\n");

            /* Store trimmed command text in settings array */
            Settings.MacroCommand.Text[CurrentButtonValue + CurrentRadioValue + MacroBank] = MacroBox.Text.Trim();
            InfoMessage($"Macro{CurrentButtonValue + CurrentRadioValue + MacroBank} set to: " + MacroBox.Text.Trim() + "\n");

            /* Save changes to configuration file */
            Settings.WriteXml();

            /* Refresh the UI with new button set */
            LoadGroupContent();
        }

        private async void LoadScript(string filePath)
        {
            // Check if the file exists
            if (!File.Exists(filePath))
            {
                InfoMessage($"Script file not found: {filePath}\n");
                AddLine($"Script file not found: {filePath}\n", 1);
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

                        // Skip empty lines
                        if (string.IsNullOrEmpty(trimmed))
                            continue;

                        char code = trimmed[0]; // first character: C, D, R, S, E, F

                        switch (code)
                        {
                            case 'S':
                                RichTextBox.Clear();
                                break;

                            case 'E':
                                if (trimmed.Length > 2 && trimmed[1] == ' ')
                                {
                                    string message = trimmed.Substring(2);
                                    AddLine(message + "\n", 1);
                                }
                                else
                                {
                                    AddLine("Invalid echo format (must be 'E <message>')\n", 1);
                                }
                                break;

                            case 'F':
                                {
                                    if (trimmed.Length > 2 && trimmed[1] == ' ')
                                    {
                                        string content = trimmed.Substring(2).Trim();

                                        // Expect format: <word><message>
                                        int firstLt = content.IndexOf('<');
                                        int firstGt = content.IndexOf('>');

                                        if (firstLt >= 0 && firstGt > firstLt)
                                        {
                                            string wordToFind = content.Substring(firstLt + 1, firstGt - firstLt - 1);

                                            // Check for optional message
                                            int secondLt = content.IndexOf('<', firstGt + 1);
                                            int secondGt = content.IndexOf('>', firstGt + 1);
                                            string message = "Found"; // default message

                                            if (secondLt >= 0 && secondGt > secondLt)
                                            {
                                                message = content.Substring(secondLt + 1, secondGt - secondLt - 1);
                                            }

                                            // Search in all RichTextBox lines
                                            string[] richLines = RichTextBox.Lines;
                                            bool found = false;

                                            foreach (string richLine in richLines)
                                            {
                                                if (richLine.Contains(wordToFind))
                                                {
                                                    found = true;
                                                    break;
                                                }
                                            }

                                            if (found)
                                                AddLine(message + "\n", 1);
                                            else
                                                AddLine($"'{wordToFind}' not found.\n", 1);
                                        }
                                        else
                                        {
                                            AddLine("Invalid find format (must be 'F <word><message>')\n", 1);
                                        }
                                    }
                                    else
                                    {
                                        AddLine("Invalid find format (must be 'F <word><message>')\n", 1);
                                    }
                                }
                                break;


                            case 'C':
                                if (trimmed.Length > 2 && trimmed[1] == ' ')
                                {
                                    string buffer = trimmed.Substring(2);
                                    if (Port.IsOpen && !string.IsNullOrEmpty(buffer))
                                    {
                                        string lineEnding = Port.Send(buffer);

                                        if (!ComboBox.Items.Contains(buffer))
                                            ComboBox.Items.Add(buffer);

                                        ComboBox.Focus();
                                    }
                                    else
                                    {
                                        string msg = "The serial port is not open. Cannot send command from script.\n";
                                        InfoMessage(msg);
                                        AddLine(msg, 1);
                                    }
                                }
                                else
                                {
                                    string msg = $"Invalid command format (must be 'C <command>'): {trimmed}\n";
                                    InfoMessage(msg);
                                    AddLine(msg, 1);
                                }
                                break;

                            case 'D':
                                if (trimmed.Length > 2 && trimmed[1] == ' ')
                                {
                                    string delayText = trimmed.Substring(2);
                                    if (int.TryParse(delayText, out int delayMs))
                                    {
                                        await Task.Delay(delayMs);
                                    }
                                    else
                                    {
                                        string msg = $"Invalid delay in script: {trimmed}\n";
                                        InfoMessage(msg);
                                        AddLine(msg, 1);
                                    }
                                }
                                else
                                {
                                    string msg = $"Invalid delay format (must be 'D <ms>'): {trimmed}\n";
                                    InfoMessage(msg);
                                    AddLine(msg, 1);
                                }
                                break;

                            case 'R':
                                {
                                    int spaceIndex = trimmed.IndexOf(' ');
                                    if (spaceIndex > 1)
                                    {
                                        string repeatStr = trimmed.Substring(1, spaceIndex - 1);
                                        string rest = trimmed.Substring(spaceIndex + 1).Trim();

                                        if (int.TryParse(repeatStr, out int repeatCount))
                                        {
                                            if (repeatCount < 1 || repeatCount > 99)
                                            {
                                                string msg = $"Repeat count must be between 1 and 99: {trimmed}\n";
                                                InfoMessage(msg);
                                                AddLine(msg, 1); // only show error
                                                break;
                                            }

                                            // Check for optional delay at the start
                                            int delayMs = 0;
                                            string command = rest;

                                            if (rest.StartsWith("D"))
                                            {
                                                int delaySpace = rest.IndexOf(' ');
                                                if (delaySpace > 1)
                                                {
                                                    string delayStr = rest.Substring(1, delaySpace - 1);
                                                    command = rest.Substring(delaySpace + 1);

                                                    if (!int.TryParse(delayStr, out delayMs))
                                                    {
                                                        string msg = $"Invalid delay in repeat command: {trimmed}\n";
                                                        InfoMessage(msg);
                                                        AddLine(msg, 1); // only show error
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    string msg = $"Invalid delay format in repeat command: {trimmed}\n";
                                                    InfoMessage(msg);
                                                    AddLine(msg, 1); // only show error
                                                    break;
                                                }
                                            }

                                            // Execute repeat loop silently
                                            for (int i = 0; i < repeatCount; i++)
                                            {
                                                if (delayMs > 0)
                                                    await Task.Delay(delayMs);

                                                if (Port.IsOpen && !string.IsNullOrEmpty(command))
                                                {
                                                    string lineEnding = Port.Send(command);

                                                    if (!ComboBox.Items.Contains(command))
                                                        ComboBox.Items.Add(command);

                                                    // Removed echo: do not call AddLine for normal execution
                                                    ComboBox.Focus();
                                                }
                                                else
                                                {
                                                    string msg = "The serial port is not open. Cannot send command from script.\n";
                                                    InfoMessage(msg);
                                                    AddLine(msg, 1); // only show error
                                                    break;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            string msg = $"Invalid repeat number in script: {trimmed}\n";
                                            InfoMessage(msg);
                                            AddLine(msg, 1); // only show error
                                        }
                                    }
                                    else
                                    {
                                        string msg = $"Invalid repeat format (must be 'R<num> <command>'): {trimmed}\n";
                                        InfoMessage(msg);
                                        AddLine(msg, 1); // only show error
                                    }
                                }
                                break;

                            default:
                                string unknownMsg = $"Unknown script code '{code}' in line: {trimmed}\n";
                                InfoMessage(unknownMsg);
                                AddLine(unknownMsg, 1);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"Error reading script file: {ex.Message}\n";
                InfoMessage(msg);
                AddLine(msg, 1);
            }
        }




        /*----------------------------------------------------------
         * SendButton_Click
         * 
         * Handles data sending via serial port
         * Shows data in RichTextBox
         *---------------------------------------------------------*/
        private void SendButton_Click(object sender, EventArgs e)
        {
            /* Get the command from the ComboBox */
            string buffer = ComboBox.Text;

            /* COMMANDS */
            if (!string.IsNullOrEmpty(buffer) && buffer[0] == '>')
            {
                AddLine(buffer + "\n", 1);
                if (buffer == ">help")
                {
                    AddLine(
                            ">q       - Quit application\n" +
                            ">cls     - Clear RichTextBox\n" +
                            ">welcome - Show welcome message\n" +
                            ">l       - List available COM ports\n" +
                            ">c       - Connect to selected COM port\n" +
                            ">a       - Auto-connect to available COM port\n" +
                            ">load    - Load and run a script file\n" +
                            ">help    - Show this help message\n", 1
                        );
                }
                else if (buffer.StartsWith(">load"))
                {
                    // Ensure there's a space and a filename
                    if (buffer.Length > 6 && buffer[5] == ' ')
                    {
                        string scriptFile = buffer.Substring(6).Trim(); // get everything after ">load "
                        if (!string.IsNullOrEmpty(scriptFile))
                        {
                            LoadScript(scriptFile); // call your async LoadScript method
                        }
                        else
                        {
                            AddLine("Usage: >load <scriptFile>\n", 1);
                        }
                    }
                    else
                    {
                        AddLine("Usage: >load <scriptFile>\n", 1);
                    }
                }
                else if (buffer == ">q")
                {
                    this.Close();
                }
                else if (buffer == ">cls")
                {
                    RichTextBox.Clear();
                }
                else if (buffer == ">welcome")
                {
                    WelcomeMsg();
                }
                else if (buffer == ">l")
                {
                    ScanCOMsToolStripMenuItem_Click(this, EventArgs.Empty);
                }
                else if (buffer == ">c")
                {
                    if (buffer.Length > 3 && buffer[3] == 'C')  
                    {
                        // do something
                    }
                    else
                        ConnectButton_Click(this, EventArgs.Empty);
                }
                else if (buffer == ">a")
                {
                    AutoConnectButton_Click(this, EventArgs.Empty);
                }


                /* Refocus the ComboBox for quick user input */
                ComboBox.Focus();
            }
            else
            {
                /* Check if port is open */
                if (Port.IsOpen)
                {
                    /* Proceed only if the command is not empty */
                    if (!string.IsNullOrEmpty(buffer))
                    {
                        /* Send the command and get the appended line ending */
                        string lineEnding = Port.Send(buffer);

                        /* Add the command to ComboBox history if not already there */
                        if (!ComboBox.Items.Contains(buffer))
                        {
                            ComboBox.Items.Add(buffer);
                        }

                        /* If local echo is enabled, display the sent command */
                        if (Settings.RichTextBox.LocalEcho)
                        {
                            AddLine(buffer + lineEnding, 0);
                        }

                        /* Refocus the ComboBox for quick user input */
                        ComboBox.Focus();
                    }
                }

                else
                    /* Inform the user if the serial port is not open */
                    InfoMessage("The serial port is not open. Please open a port before sending the macro.\n");
            }             
        }

        /*----------------------------------------------------------
         * ClearButton_Click
         * 
         * Clears the RichTextBox content
         *---------------------------------------------------------*/
        private void ClearButton_Click(object sender, EventArgs e) => RichTextBox.Clear();

        /*----------------------------------------------------------
         * OptionsButton_Click
         * 
         * Opens the Serial Port Options configuration dialog
         *---------------------------------------------------------*/
        private void OptionsButton_Click(object sender, EventArgs e)
        {
            SerialPortOptionsForm OptionsForm = new SerialPortOptionsForm();
            OptionsForm.ShowDialog();
            DTRPictureBox.BackColor = Settings.Port.Dtr ? Color.Green : Color.Transparent;
            RTSPictureBox.BackColor = Settings.Port.Rts ? Color.Green : Color.Transparent;
            UpdateAppendButtons();
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
            AutoConnectPictureBox.BackColor = AutoConnectTimer.Enabled ? Color.Green : Color.Transparent;
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
            SerialPictureBox.BackColor = Port.IsOpen ? Color.Green : Color.Transparent;
            AutoConnectTimer.Enabled = false;
            AutoConnectButton.ForeColor = Color.Black;
            AutoConnectPictureBox.BackColor = Color.Transparent;
        }

        /*----------------------------------------------------------
         * SwapButton_MouseDown
         * 
         * Handles circular navigation through button/page groups
         *---------------------------------------------------------*/
        private void SwapButton_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                /* Left click: navigate forward */
                PageIndex = (PageIndex + 8) % 16; //set 16 to 32 for 4 pages
                MacroBank = (MacroBank + 128) % 256;// set 256 to 512 for 4 pages
            }
            else if (e.Button == MouseButtons.Left)
            {
                /* Right click: navigate backward */
                PageIndex = (PageIndex - 8 + 16) % 16; // set 16 and 16 to 32 and 32 for 4 pages
                MacroBank = (MacroBank - 128 + 256) % 256; // set 256 and 256 to 512 and 512 for 4 pages
            }

            if(MacroBank == 0)
                MacrosGroupBox.Text = "Macros   " + MacroBank.ToString() + " - "+ (MacroBank + 127).ToString();
            else
                MacrosGroupBox.Text = "Macros " + MacroBank.ToString() + " - " + (MacroBank + 127).ToString();


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
         * Sends the selected macro command if not in edit mode.
         * If in edit mode, loads the macro into the editor UI.
         *---------------------------------------------------------*/
        public async void MacroButton_Click(int macroIndex)
        {
            /* If not in macro editing mode, proceed with sending the macro */
            if (!MFlag[0] && !MFlag[1] && !MFlag[2] && !MFlag[3] && !MFlag[4] && !MacroEditingFlag)
            {
                /* Check if port is open */
                if(Port.IsOpen)
                {
                    /* Retrieve the macro command based on current index and bank */
                    string command = Settings.MacroCommand.Text[macroIndex + CurrentRadioValue + MacroBank];

                    /* Send the command only if it's not empty */
                    if (!string.IsNullOrEmpty(command))
                    {
                        // --- NEW: Handle >load commands ---
                        if (command.StartsWith(">load"))
                        {
                            // Call the existing LoadScript method
                            string scriptFile = command.Length > 6 ? command.Substring(6).Trim() : "";
                            if (!string.IsNullOrEmpty(scriptFile))
                            {
                                LoadScript(scriptFile);
                            }
                            else
                            {
                                AddLine("Usage: >load <scriptFile>\n", 1);
                            }
                        }
                        else
                        {
                            string lineEnding = Port.Send(command);

                            if (Settings.RichTextBox.LocalEcho)
                            {
                                AddLine(command + lineEnding, 0);
                            }

                            await Task.Delay(50);
                        }                         
                    }

                    else
                    {
                        InfoMessage("The selected macro is empty. Please configure a command before using this button.\n");
                    }
                } 
                
                else
                {
                    /* Inform the user if the serial port is not open */
                    InfoMessage("The serial port is not open. Please open a port before sending the macro.\n");
                }
            }
            if (MFlag[0])
                SetMacro(ref MFlag[0], M1SetToolStripMenuItem, M1ToolStripMenuItem, ref CurrentMRepeat[0], macroIndex);
            else if (MFlag[1])
                SetMacro(ref MFlag[1], M2SetToolStripMenuItem, M2ToolStripMenuItem, ref CurrentMRepeat[1], macroIndex);
            else if (MFlag[2])
                SetMacro(ref MFlag[2], M3SetToolStripMenuItem, M3ToolStripMenuItem, ref CurrentMRepeat[2], macroIndex);
            else if (MFlag[3])
                SetMacro(ref MFlag[3], M4SetToolStripMenuItem, M4ToolStripMenuItem, ref CurrentMRepeat[3], macroIndex);
            else if (MFlag[4])
                SetMacro(ref MFlag[4], M5SetToolStripMenuItem, M5ToolStripMenuItem, ref CurrentMRepeat[4], macroIndex);
            else if (MacroEditingFlag)
            {
                /* In macro editing mode: load macro details into the UI for editing */
                MacroBox.Text = Settings.MacroCommand.Text[macroIndex + CurrentRadioValue + MacroBank];
                MacroTextBox.Text = Settings.MacroText.Text[macroIndex + CurrentRadioValue + MacroBank];
                PageBox.Text = Settings.Page.Text[CurrentRadioValue / 16 + PageIndex];
                CurrentButtonValue = macroIndex;
            }
        }

        /*----------------------------------------------------------
         * MacroButton_MouseDown
         * 
         * Toggles macro editing mode on right-click.
         *---------------------------------------------------------*/
        private void MacroButton_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                MacroEditingFlag = !MacroEditingFlag;
                SetMacroSetMode(MacroEditingFlag);
            }
        }

        /*----------------------------------------------------------
         * MinusButton_Click
         * 
         * Simulates a left-click on the Swap button to navigate
         * macros/pages backward. Internally calls SwapButton_MouseDown.
        *---------------------------------------------------------*/
        private void MinusButton_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouseArgs = new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0);
            SwapButton_MouseDown(sender, mouseArgs);
        }

        /*----------------------------------------------------------
         * PlusButton_Click
         * 
         * Simulates a right-click on the Swap button to navigate
         * macros/pages forward. Internally calls SwapButton_MouseDown.
        *---------------------------------------------------------*/
        private void PlusButton_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouseArgs = new MouseEventArgs(MouseButtons.Right, 1, 0, 0, 0);
            SwapButton_MouseDown(sender, mouseArgs);
        }

        /*----------------------------------------------------------
         * DTRButton_Click
         * 
         * Toggles the DTR (Data Terminal Ready) line for the serial
         * port. Updates button color to reflect state and saves
         * the setting to XML. Applies change immediately if port
         * is open.
        *---------------------------------------------------------*/
        private void DTRButton_Click(object sender, EventArgs e)
        {

            Settings.Port.Dtr = !Settings.Port.Dtr;
            DTRPictureBox.BackColor = Settings.Port.Dtr ? Color.Green : Color.Transparent;

            if (Serial.Instance.IsOpen)
                Serial.Instance.SetDtr(Settings.Port.Dtr);

            Settings.WriteXml();
        }

        /*----------------------------------------------------------
         * RTSButton_Click
         * 
         * Toggles the RTS (Request To Send) line for the serial
         * port. Updates button color to reflect state and saves
         * the setting to XML. Applies change immediately if port
         * is open.
        *---------------------------------------------------------*/
        private void RTSButton_Click(object sender, EventArgs e)
        {
            Settings.Port.Rts = !Settings.Port.Rts;
            RTSPictureBox.BackColor = Settings.Port.Rts ? Color.Green : Color.Transparent;

            if (Serial.Instance.IsOpen)
                Serial.Instance.SetRts(Settings.Port.Rts);

            Settings.WriteXml();
        }

        /*----------------------------------------------------------
         * ComboBox
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * ComboBox_Enter
         * 
         * Disables clipboard context menu items when the ComboBox
         * gains focus, preventing operations like copy/paste while
         * entering commands.
         *---------------------------------------------------------*/
        private void ComboBox_Enter(object sender, EventArgs e)
        {
            UndoToolStripMenuItem.Enabled =
                PasteToolStripMenuItem.Enabled = 
                CopyToolStripMenuItem.Enabled = 
                CutToolStripMenuItem.Enabled = 
                DeleteToolStripMenuItem.Enabled =
                SelectAllToolStripMenuItem.Enabled = false;
        }


        /*----------------------------------------------------------
         * ComboBox_KeyDown
         * 
         * Handles Enter key within the ComboBox. Sends the current
         * command by calling SendButton_Click and prevents the beep
         * or focus shift by suppressing the key press.
         *---------------------------------------------------------*/
        private void ComboBox_KeyDown(object sender, KeyEventArgs keyboard)
        {
            if (keyboard.KeyCode == Keys.Enter)
            {
                SendButton_Click(sender, keyboard);
                keyboard.Handled = true;
                keyboard.SuppressKeyPress = true;
            }
        }

        /*----------------------------------------------------------
         * ComboBox_Leave
         * 
         * Re-enables clipboard context menu items when the ComboBox
         * loses focus, restoring standard edit options.
         *---------------------------------------------------------*/
        private void ComboBox_Leave(object sender, EventArgs e)
        {
            UndoToolStripMenuItem.Enabled =
                PasteToolStripMenuItem.Enabled =
                CopyToolStripMenuItem.Enabled =
                CutToolStripMenuItem.Enabled =
                DeleteToolStripMenuItem.Enabled =
                SelectAllToolStripMenuItem.Enabled = true;
        }

        /*----------------------------------------------------------
         * Events
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
            SerialPictureBox.BackColor = Port.IsOpen ? Color.Green : Color.Transparent;
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
            SerialPictureBox.BackColor = Port.IsOpen ? Color.Green : Color.Transparent;
            AutoConnectPictureBox.BackColor = AutoConnectPictureBox.BackColor == Color.Transparent ? Color.Green : Color.Transparent;
            if (!Port.IsOpen) Port.Open();
        }

        /*----------------------------------------------------------
         * M1Tick
         * 
         * Timer tick handler for M1 macro repeat.
         *---------------------------------------------------------*/
        private void MGenericTick(object sender, EventArgs e)
        {
            if (!(sender is Timer timer)) return;

            int index = (int)timer.Tag;

            ToolStripMenuItem menuItem = null;
            switch (index)
            {
                case 0: menuItem = M1ToolStripMenuItem; break;
                case 1: menuItem = M2ToolStripMenuItem; break;
                case 2: menuItem = M3ToolStripMenuItem; break;
                case 3: menuItem = M4ToolStripMenuItem; break;
                case 4: menuItem = M5ToolStripMenuItem; break;
            }

            if (menuItem != null)
            {
                MacroTick(CurrentMRepeat[index], menuItem, ref MFlicker[index]);
            }
        }

        /*----------------------------------------------------------
         * Helper Functions
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
        private void AddLine(string line, int type)
        {
            /* Set the selection color based on message type */
            RichTextBox.SelectionColor = type == 0 ? TransmitColor : ReceiveColor;

            string buffer = "";

            if (Settings.RichTextBox.HexOutput)
            {
                byte[] lineBytes = Encoding.UTF8.GetBytes(line + buffer);
                string lineAsString = BitConverter.ToString(lineBytes).Replace("-", " ");
                RichTextBox.AppendText($" [{lineAsString}]");
            }
            else
            {
                RichTextBox.AppendText(line + buffer);
            }

            // Blink TX or RX indicator
            PictureBox targetBox = (type == 0) ? TXPictureBox : RXPictureBox;

            targetBox.BackColor = Color.Green;

            var pulseTimer = new System.Windows.Forms.Timer { Interval = 60 };
            pulseTimer.Tick += (s, e) =>
            {
                targetBox.BackColor = Color.Transparent;
                pulseTimer.Stop();
                pulseTimer.Dispose();
            };
            pulseTimer.Start();

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
        private async void WelcomeMsg()
        {
            RichTextBox.BackColor = Color.FromArgb(30, 30, 30);
            string archSuffix = Environment.Is64BitProcess ? " x64" : " x86";
            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3) + archSuffix;
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
                $"                                                                   Version: EXPERIMENTAL {version}"
            };
            /* Colors to cycle through */
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
            RichTextBox.BackColor = BackgroundColor;
        }

        /*----------------------------------------------------------
         * SetAdvancedMode
         * 
         * Enables or disables advanced UI controls and flags.
         *---------------------------------------------------------*/
        private void SetAdvancedMode(bool enable)
        {
            Settings.Form.AdvancedMode = AdvancedModeToolStripMenuItem.Checked = enable;

            M1ToolStripMenuItem.Visible = M1ToolStripMenuItem.Enabled =
            M2ToolStripMenuItem.Visible = M2ToolStripMenuItem.Enabled =
            M3ToolStripMenuItem.Visible = M3ToolStripMenuItem.Enabled =
            M4ToolStripMenuItem.Visible = M4ToolStripMenuItem.Enabled =
            M5ToolStripMenuItem.Visible = M5ToolStripMenuItem.Enabled = enable;
        }

        /*----------------------------------------------------------
         * SetMacroSetMode
         * 
         * Enables or disables macro UI controls and flags.
         *---------------------------------------------------------*/
        private void SetMacroSetMode(bool enable)
        {
            MacroSetPanel.Visible = MacroSetPanel.Enabled = MacroModeToolStripMenuItem.Checked = enable;

            UndoToolStripMenuItem.Enabled = CutToolStripMenuItem.Enabled =
            CopyToolStripMenuItem.Enabled = PasteToolStripMenuItem.Enabled =
            DeleteToolStripMenuItem.Enabled = SelectAllToolStripMenuItem.Enabled = !enable;

            if(Settings.Form.ExternalTools)
                if(enable)
                    MacroSetPanel.Location = new Point(MacroSetPanel.Location.X, MacroSetPanel.Location.Y - 38);
                else
                    MacroSetPanel.Location = new Point(MacroSetPanel.Location.X, MacroSetPanel.Location.Y + 38);
        }

        /*----------------------------------------------------------
         * MacroTick
         * 
         * Toggles flicker effect and sends macro command if port is open.
         *---------------------------------------------------------*/
        private void MacroTick(int current, ToolStripMenuItem item, ref bool flicker)
        {
            flicker = !flicker;
            item.BackColor = flicker ? Color.Green : SystemColors.Control;
            /* Check if port is open */
            if (Port.IsOpen)
            {
                /* Retrieve the macro command based on current index and bank */
                string command = Settings.MacroCommand.Text[current];

                /* Send the command only if it's not empty */
                if (!string.IsNullOrEmpty(command))
                {
                    string lineEnding = Port.Send(command);

                    if (Settings.RichTextBox.LocalEcho)
                        AddLine(command + lineEnding, 0);
                }
            }
        }

        /*----------------------------------------------------------
         * ErrorMessage
         * 
         * Displays an error message in the RichTextBox.
         *---------------------------------------------------------*/
        private void ErrorMessage(string message)
        {
            RichTextBox.SelectionColor = TransmitColor;
            RichTextBox.AppendText("[ Error ] " + message);
        }

        /*----------------------------------------------------------
         * InfoMessage
         * 
         * Displays an informational message in the RichTextBox.
         *---------------------------------------------------------*/
        private void InfoMessage(string message)
        {
            RichTextBox.SelectionColor = TransmitColor;
            RichTextBox.AppendText("[ Info ] " + message);
        }        

        /*----------------------------------------------------------
         * SetMacro
         * 
         * Sets up a macro configuration by updating the display text,
         * resetting flags and colors, and updating the repeat count
         * based on the selected macro index combined with current 
         * radio and macro bank values.
         *---------------------------------------------------------*/
        private void SetMacro(ref bool flag, ToolStripMenuItem setItem, ToolStripMenuItem mainItem, ref int currentRepeat, int macroIndex)
        {
            int index = macroIndex + CurrentRadioValue + MacroBank;
            setItem.Text = Settings.MacroText.Text[index];
            currentRepeat = index;
            flag = false;
            mainItem.BackColor = SystemColors.Control;
            InfoMessage($"Macro{setItem.Name[1]} set to: {Settings.MacroText.Text[index]}\n");
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

        private void NonePictureBox_Click(object sender, EventArgs e)
        {
            Settings.Port.Append = Settings.Port.AppendType.AppendNothing;
            UpdateAppendButtons();
        }

        private void CRPictureBox_Click(object sender, EventArgs e)
        {
            Settings.Port.Append = Settings.Port.AppendType.AppendCR;
            UpdateAppendButtons();
        }

        private void LFPictureBox_Click(object sender, EventArgs e)
        {
            Settings.Port.Append = Settings.Port.AppendType.AppendLF;
            UpdateAppendButtons();
        }

        private void CRLFPictureBox_Click(object sender, EventArgs e)
        {
            Settings.Port.Append = Settings.Port.AppendType.AppendCRLF;
            UpdateAppendButtons();
        }

        private void UpdateAppendButtons()
        {
            NonePictureBox.BackColor =
            CRPictureBox.BackColor =
            LFPictureBox.BackColor =
            CRLFPictureBox.BackColor = Color.Transparent;

            PictureBox active = null;

            switch (Settings.Port.Append)
            {
                case Settings.Port.AppendType.AppendNothing:
                    active = NonePictureBox;
                    break;
                case Settings.Port.AppendType.AppendCR:
                    active = CRPictureBox;
                    break;
                case Settings.Port.AppendType.AppendLF:
                    active = LFPictureBox;
                    break;
                case Settings.Port.AppendType.AppendCRLF:
                    active = CRLFPictureBox;
                    break;
            }

            if (active != null)
                active.BackColor = Color.Green;
        }
    }
}
