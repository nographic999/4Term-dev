/* Main_Form.cs -- the main window: connecting, sending, macros, scripts and
   showing received data.  The menu handlers are in MenuStrip.cs and the hex
   view in HexView.cs.

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

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
    /* The main window.  */
    public partial class Main_Form : Form, IScriptHost
    {
        /* Signature for calling ChangeStatus and DataReceive on the UI
           thread.  */
        internal delegate void StringDelegate(string data);
        /* The serial port connection.  */
        private readonly Serial Port = Serial.Instance;
        /* First page of the current page group, a multiple of
           Settings.Layout.PagesPerGroup.  */
        private int PageIndex = 0;
        /* First macro of the current group's bank, a multiple of
           Settings.Layout.MacrosPerGroup.  */
        private int MacroBank = 0;
        /* Offset of the selected page within the bank: the page number times
           Settings.Layout.ButtonsPerPage.  */
        private int CurrentRadioValue = 0;
        /* File used by File > Save, or null if there is none.  */
        private string FilePath = null;

        /* Macro offset of each page radio button.  */
        private readonly Dictionary<string, int> radioButtonMapping =
            Enumerable.Range(0, Settings.Layout.PagesPerGroup)
                      .ToDictionary(i => "RadioButton" + i, i => i * Settings.Layout.ButtonsPerPage);

        /* Tips of the macro buttons: their commands.  */
        private readonly ToolTip MacroTips = new ToolTip();
        /* The macro buffer, shown over the output window; see
           MacroBuffer.cs.  */
        private readonly MacroBufferPane MacroBuffer = new MacroBufferPane();

        /* Timers.  */

        /* Tries to open the port while auto-connect is on.  */
        private readonly Timer AutoConnectTimer = new Timer();
        /* Macros that repeat; see MacroRepeater.cs.  */
        private readonly MacroRepeater Repeater;
        /* View > Stop Repeats, and the indicator above the macro buttons.  */
        private ToolStripMenuItem StopRepeatsMenuItem;
        private Label RepeatingLabel;

        /* Receive state.  */

        /* True while the welcome animation owns the output window.  */
        private bool WelcomeRunning = false;
        /* Data received while the welcome animation runs, shown
           afterwards.  */
        private readonly StringBuilder PendingReceive = new StringBuilder();
        /* Decodes received bytes as UTF-8, keeping a character that is split
           across chunks intact.  */
        private readonly Decoder ReceiveDecoder = Encoding.UTF8.GetDecoder();

        /* Colors from the settings.  */

        /* Background of the output window.  */
        Color BackgroundColor = Color.Empty;
        /* Sent data and 4Term's own messages.  */
        Color TransmitColor = Color.Empty;
        /* Received data and script output.  */
        Color ReceiveColor = Color.Empty;

        /* Create the main window and subscribe to the serial port events.  */
        public Main_Form()
        {
            /* Initialize all UI components defined in the designer.  */
            InitializeComponent();

            /* Put the output window and the macro buffer in one split.  */
            CreateMacroBuffer();

            /* Repeating macros redraw their buttons when they start or
               stop.  */
            Repeater = new MacroRepeater(SendRepeated);
            Repeater.Changed += LoadGroupContent;
            CreateStopRepeatsMenuItem();

            /* Add the Hex View toggle to the View menu.  */
            CreateHexViewMenuItem();

            /* Add finding, timestamps, highlights and logging.  */
            CreateOutputTools();

            /* Subscribe to the serial port events.  */
            Port.StatusChanged += ChangeStatus;
            Port.DataReceived += DataReceive;
        }

        /* Load the settings and apply them before the window is first
           shown.  */
        private void Main_Form_Load(object sender, EventArgs e)
        {
            /* Load settings.  */
            Settings.ReadXml();

            /* Set the window size.  */
            Size = new Size(Settings.Window.Width, Settings.Window.Height);

            /* Initialize colors, font, word wrap and scrolling.  */
            ApplyDisplaySettings();

            /* Show the DTR and RTS states and the swap button.  */
            UpdateLineIndicators();
            SwapButton.Visible = SwapButton.Enabled = Settings.Program.IsSwapButtonVisible;

            /* Set up the line ending indicators and the macro buttons.  */
            UpdateAppendButtons();
            AssignButtonClickHandlers();
            LoadGroupContent();               
        }

        /* Set up the timers and show the welcome animation once the window is
           visible.  */
        private void Main_Form_Shown(object sender, EventArgs e)
        {
            /* Set up the auto-connect timer; it runs while auto-connect is
               on.  */
            AutoConnectTimer.Interval = 100;
            AutoConnectTimer.Tick += ReconnectTick;

            /* Show the welcome animation on the first run, or on every start
               if that is enabled.  */
            if (Settings.Program.FirstStart || Settings.Program.ShowWelcomeMessage)
                WelcomeMsg();
            else
            {
                RichTextBox.Clear();
                RichTextBox.BackColor = BackgroundColor;
                RichTextBox.ForeColor = TransmitColor;             
            }

            /* Reset FirstStart flag and save settings on first run.  */
            if (Settings.Program.FirstStart)
            {
                Settings.Program.FirstStart = false;
                Settings.WriteXml();
            }                  
        }

        /* Clean up as the window closes: stop the events and the macro timers
           and close the port.  */
        private void Main_Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            /* Unsaved macros or highlights: ask first, and stay open on
               Cancel.  */
            if ((MacroBufferVisible && !MacroBuffer.ConfirmLeave()) ||
                (HighlightPaneVisible && !Highlights.ConfirmLeave()))
            {
                e.Cancel = true;
                return;
            }

            /* Close the log; its setting stays, so it starts again next
               time.  */
            Log.Close();

            /* Stop receiving callbacks before the form goes away.  */
            Port.StatusChanged -= ChangeStatus;
            Port.DataReceived -= DataReceive;

            /* Stop macro repeat timers.  */
            Repeater.StopAll();

            if (Port.IsOpen)
                Port.Close();
        }

        /* Run the script in the file FILEPATH; see Script for the
           commands.  */
        private async void LoadScript(string filePath)
        {
            await new Script(this).RunAsync(filePath);
        }

        /* What a running script can do with this window; see IScriptHost.  */
        void IScriptHost.ClearOutput() => RichTextBox.Clear();
        void IScriptHost.Show(string text) => AddLine(text, 1);
        string[] IScriptHost.OutputLines => RichTextBox.Lines;
        bool IScriptHost.PortOpen => Port.IsOpen;
        string IScriptHost.Send(string command) => Port.Send(command);

        void IScriptHost.Remember(string command)
        {
            if (!ComboBox.Items.Contains(command))
                ComboBox.Items.Add(command);

            ComboBox.Focus();
        }




        /* Send the text in the command box, or run it as a 4Term command if it
           starts with '>' (see >help).  Sent commands are added to the box's
           history.  */
        private void SendButton_Click(object sender, EventArgs e)
        {
            /* Get the command from the ComboBox.  */
            string buffer = ComboBox.Text;

            /* 4Term commands start with '>'.  */
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
                    /* Expect a space and a file name.  */
                    if (buffer.Length > 6 && buffer[5] == ' ')
                    {
                        string scriptFile = buffer.Substring(6).Trim(); /* Everything after ">load ".  */
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
                    ConnectButton_Click(this, EventArgs.Empty);
                }
                else if (buffer == ">a")
                {
                    AutoConnectButton_Click(this, EventArgs.Empty);
                }


                /* Refocus the ComboBox for quick user input.  */
                ComboBox.Focus();
            }
            else
            {
                /* Check whether the port is open.  */
                if (Port.IsOpen)
                {
                    /* Proceed only if the command is not empty.  */
                    if (!string.IsNullOrEmpty(buffer))
                    {
                        /* Send the command and get the appended line
                           ending.  */
                        string lineEnding = Port.Send(buffer);

                        /* Add the command to ComboBox history if not already
                           there.  */
                        if (!ComboBox.Items.Contains(buffer))
                        {
                            ComboBox.Items.Add(buffer);
                        }

                        /* If local echo is enabled, display the sent
                           command.  */
                        if (lineEnding != null && Settings.Output.LocalEcho)
                        {
                            AddLine(buffer + lineEnding, 0);
                        }

                        /* Refocus the ComboBox for quick user input.  */
                        ComboBox.Focus();
                    }
                }

                else
                    /* Inform the user if the serial port is not open.  */
                    InfoMessage("The serial port is not open. Please open a port before sending the macro.\n");
            }             
        }

        /* Clear the output window.  */
        private void ClearButton_Click(object sender, EventArgs e) => RichTextBox.Clear();

        /* Show the serial options dialog.  */
        private void OptionsButton_Click(object sender, EventArgs e) => ShowSerialOptions();

        /* Switch auto-connect on or off.  While it is on, 4Term tries to open
           the port every 100 ms.  */
        private void AutoConnectButton_Click(object sender, EventArgs e)
        {
            AutoConnectTimer.Enabled = !AutoConnectTimer.Enabled;
            AutoConnectButton.ForeColor = AutoConnectTimer.Enabled ? Color.Red : Color.Black;
            AutoConnectPictureBox.BackColor = AutoConnectTimer.Enabled ? Color.Green : Color.Transparent;
        }

        /* Connect or disconnect, and switch auto-connect off.  */
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

        /* Switch to the other page group and macro bank, keeping the page
           number within the group.  A right-click moves forward, a
           left-click backward, wrapping around at the ends.  */
        private void SwapButton_MouseDown(object sender, MouseEventArgs e)
        {
            const int Pages = Settings.Layout.Pages;
            const int PageStep = Settings.Layout.PagesPerGroup;

            if (e.Button == MouseButtons.Right)
                GoToPage((CurrentPage + PageStep) % Pages);
            else if (e.Button == MouseButtons.Left)
                GoToPage((CurrentPage - PageStep + Pages) % Pages);
        }

        /* Select the page of the clicked radio button and show its macros.
           If the macro buffer has unsaved changes and the user cancels, the
           old page stays selected.  */
        private void RadioButton_Click(object sender, EventArgs e)
        {
            if (sender is RadioButton selectedRadioButton && radioButtonMapping.TryGetValue(selectedRadioButton.Name, out int radioValue))
                GoToPage(PageIndex + radioValue / Settings.Layout.ButtonsPerPage);
        }

        /* Absolute number of the page shown, from 0.  */
        private int CurrentPage => PageIndex + CurrentRadioValue / Settings.Layout.ButtonsPerPage;

        /* Show page PAGE, an absolute page number: select its group and radio
           button, show its macros and, if the macro buffer is open, load it
           there.  Return false, changing nothing, if the buffer has unsaved
           changes and the user cancels.  */
        private bool GoToPage(int page)
        {
            int current = CurrentPage;
            if (page != current && MacroBufferVisible && !MacroBuffer.ConfirmLeave())
            {
                SelectPageRadioButton();
                return false;
            }

            int group = page / Settings.Layout.PagesPerGroup;
            PageIndex = group * Settings.Layout.PagesPerGroup;
            MacroBank = group * Settings.Layout.MacrosPerGroup;
            CurrentRadioValue = page % Settings.Layout.PagesPerGroup * Settings.Layout.ButtonsPerPage;


            SelectPageRadioButton();
            if (MacroBufferVisible && MacroBuffer.Page != page)
                MacroBuffer.ShowPage(page);
            LoadGroupContent();
            return true;
        }

        /* Check the radio button of the page shown.  */
        private void SelectPageRadioButton()
        {
            int number = CurrentRadioValue / Settings.Layout.ButtonsPerPage;
            if (Controls.Find($"RadioButton{number}", true).FirstOrDefault() is RadioButton radioButton)
                radioButton.Checked = true;
        }

        /* Ctrl+1 to Ctrl+8 click the macro buttons of the first row, and
           Ctrl+Shift+1 to Ctrl+Shift+8 those of the second, except while
           editing in the macro buffer.  */
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys code = keyData & Keys.KeyCode;
            Keys modifiers = keyData & Keys.Modifiers;
            if (code >= Keys.D1 && code <= Keys.D8 && !TypingInField() &&
                (modifiers == Keys.Control || modifiers == (Keys.Control | Keys.Shift)))
            {
                int button = code - Keys.D1 + (modifiers == Keys.Control ? 0 : 8);
                if (button < Settings.Layout.ButtonsPerPage)
                {
                    MacroButton_Click(button);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /* True while the user types in the macro buffer, where Ctrl+1 to
           Ctrl+8 would send the saved macro rather than the one being
           edited.  The command box keeps the shortcuts: the focus is
           usually there.  */
        private bool TypingInField() => MacroBufferVisible && MacroBuffer.ContainsFocus;

        /* Handle a click on macro button MACROINDEX, numbered from 0 on the
           page: run its saved macro, or, if it has a repeat interval, start
           or stop repeating it.  */
        public void MacroButton_Click(int macroIndex)
        {
            int macro = macroIndex + CurrentRadioValue + MacroBank;
            int interval = MacroRepeater.IntervalOf(macro);

            if (Repeater.IsRunning(macro))
                Repeater.Stop(macro);
            else if (interval <= 0)
                RunMacroCommand(Settings.MacroCommand.Text[macro]);
            else if (string.IsNullOrWhiteSpace(Settings.MacroCommand.Text[macro]))
                InfoMessage("The selected macro is empty. Please configure a command before using this button.\n");
            else if (!Port.IsOpen)
                InfoMessage("The serial port is not open. Please open a port before sending the macro.\n");
            else
                Repeater.Start(macro, interval);
        }

        /* Add View > Stop Repeats, which stops every repeating macro, also
           those on other pages.  */
        private void CreateStopRepeatsMenuItem()
        {
            StopRepeatsMenuItem = new ToolStripMenuItem("Stop &Repeats")
            {
                Name = "StopRepeatsMenuItem",
                ShortcutKeys = Keys.Control | Keys.Shift | Keys.R,
                Enabled = false
            };
            StopRepeatsMenuItem.Click += (s, e) => Repeater.StopAll();
            int index = ViewToolStripMenuItem.DropDownItems.IndexOf(MacroModeToolStripMenuItem);
            ViewToolStripMenuItem.DropDownItems.Insert(index + 1, StopRepeatsMenuItem);

            /* The indicator sits on the group box's top edge, right of the
               group arrows; clicking it stops all repeats too.  */
            RepeatingLabel = new Label
            {
                AutoSize = true,
                Visible = false,
                Font = MacrosGroupBox.Font,
                ForeColor = Color.FromArgb(0x15, 0x80, 0x3D),
                BackColor = SystemColors.Control,
                Cursor = Cursors.Hand,
                Location = new Point(PlusButton.Right + 16, 0)
            };
            RepeatingLabel.Click += (s, e) => Repeater.StopAll();
            MacrosGroupBox.Controls.Add(RepeatingLabel);
            RepeatingLabel.BringToFront();
        }

        /* Show the macro range in the title of the macro group box and the
           number of repeating macros next to it, and enable Stop Repeats
           while any repeat.  A repeat on another page is easy to forget.  */
        private void UpdateMacrosTitle()
        {
            /* For example "Macros   0 - 127", numbers right-aligned.  */
            MacrosGroupBox.Text = $"Macros {MacroBank,3} - {MacroBank + Settings.Layout.MacrosPerGroup - 1}";
            int count = Repeater.Count;
            RepeatingLabel.Text = count == 1 ? " ~ 1 repeating - click or Ctrl+Shift+R to stop "
                                : $" ~ {count} repeating - click or Ctrl+Shift+R to stop ";
            RepeatingLabel.Visible = count > 0;
            StopRepeatsMenuItem.Enabled = count > 0;
            StopRepeatsMenuItem.Text = count > 0 ? $"Stop &Repeats ({count})" : "Stop &Repeats";
        }

        /* Send COMMAND for a repeating macro, with local echo.  Return false
           if the port is closed.  */
        private bool SendRepeated(string command)
        {
            if (!Port.IsOpen)
                return false;
            string lineEnding = Port.Send(command);
            if (lineEnding != null && Settings.Output.LocalEcho)
                AddLine(command + lineEnding, 0);
            return true;
        }

        /* Run COMMAND as a macro: run it if it is a ">load" script, otherwise
           send it.  Used by the macro buttons and by Ctrl+Enter in the macro
           buffer.  */
        private void RunMacroCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
            {
                InfoMessage("The selected macro is empty. Please configure a command before using this button.\n");
            }

            /* Scripts run without an open port (they may only echo or
               search).  */
            else if (command.StartsWith(">load"))
            {
                string scriptFile = command.Length > 6 ? command.Substring(6).Trim() : "";
                if (!string.IsNullOrEmpty(scriptFile))
                    LoadScript(scriptFile);
                else
                    AddLine("Usage: >load <scriptFile>\n", 1);
            }

            else if (!Port.IsOpen)
            {
                InfoMessage("The serial port is not open. Please open a port before sending the macro.\n");
            }

            else
            {
                string lineEnding = Port.Send(command);

                if (lineEnding != null && Settings.Output.LocalEcho)
                    AddLine(command + lineEnding, 0);
            }
        }

        /* Open the macro buffer at the right-clicked macro button.  */
        private void MacroButton_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || !(sender is Button button))
                return;

            if (int.TryParse(button.Name.Substring("MacroButton".Length), out int number))
            {
                SetMacroBuffer(true);
                MacroBuffer.FocusRow(number);
            }
        }

        /* Move to the previous page group and macro bank, like a left-click on
           the swap button.  */
        private void MinusButton_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouseArgs = new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0);
            SwapButton_MouseDown(sender, mouseArgs);
        }

        /* Move to the next page group and macro bank, like a right-click on
           the swap button.  */
        private void PlusButton_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouseArgs = new MouseEventArgs(MouseButtons.Right, 1, 0, 0, 0);
            SwapButton_MouseDown(sender, mouseArgs);
        }

        /* Toggle the DTR line, show its state and save the setting.  The
           change applies at once if the port is open.  */
        private void DTRButton_Click(object sender, EventArgs e)
        {
            Settings.Port.Dtr = !Settings.Port.Dtr;
            UpdateLineIndicators();
            Port.SetDtr(Settings.Port.Dtr);
            Settings.WriteXml();
        }

        /* Toggle the RTS line, show its state and save the setting.  The
           change applies at once if the port is open.  While RTS/CTS
           handshaking drives the line, say so and change nothing.  */
        private void RTSButton_Click(object sender, EventArgs e)
        {
            if (Port.IsOpen && Port.RtsUsedByHandshake)
            {
                InfoMessage("RTS is controlled by RTS/CTS handshaking.\n");
                return;
            }

            Settings.Port.Rts = !Settings.Port.Rts;
            UpdateLineIndicators();
            Port.SetRts(Settings.Port.Rts);
            Settings.WriteXml();
        }

        /* Show the DTR and RTS settings on their indicators.  */
        private void UpdateLineIndicators()
        {
            DTRPictureBox.BackColor = Settings.Port.Dtr ? Color.Green : Color.Transparent;
            RTSPictureBox.BackColor = Settings.Port.Rts ? Color.Green : Color.Transparent;
        }

        /* Command box.  */

        /* Disable the Edit menu's clipboard items while the command box has
           focus, so that shortcuts such as Ctrl+V act on the box rather than
           the output window.  */
        private void ComboBox_Enter(object sender, EventArgs e)
        {
            UndoToolStripMenuItem.Enabled =
                PasteToolStripMenuItem.Enabled = 
                CopyToolStripMenuItem.Enabled = 
                CutToolStripMenuItem.Enabled = 
                DeleteToolStripMenuItem.Enabled =
                SelectAllToolStripMenuItem.Enabled = false;
        }


        /* When Enter is pressed (KEYBOARD), send the command and suppress the
           key so that it does not beep.  */
        private void ComboBox_KeyDown(object sender, KeyEventArgs keyboard)
        {
            if (keyboard.KeyCode == Keys.Enter)
            {
                SendButton_Click(sender, keyboard);
                keyboard.Handled = true;
                keyboard.SuppressKeyPress = true;
            }
        }

        /* Enable the Edit menu's clipboard items again when the command box
           loses focus.  */
        private void ComboBox_Leave(object sender, EventArgs e)
        {
            UndoToolStripMenuItem.Enabled =
                PasteToolStripMenuItem.Enabled =
                CopyToolStripMenuItem.Enabled =
                CutToolStripMenuItem.Enabled =
                DeleteToolStripMenuItem.Enabled =
                SelectAllToolStripMenuItem.Enabled = true;
        }

        /* Serial port events.  */

        /* Show the connection state, and the status text BUFFER in the title
           bar.  Calls from other threads are passed on to the UI thread.  */
        public void ChangeStatus(string buffer)
        {
            if (InvokeRequired)
            {
                PostToUi(new StringDelegate(ChangeStatus), buffer);
                return;
            }
            ConnectButton.Text = Port.IsOpen ? "Disconnect" : "Connect";
            ConnectButton.ForeColor = Port.IsOpen ? Color.Red : Color.Green;
            SerialPictureBox.BackColor = Port.IsOpen ? Color.Green : Color.Transparent;
            Text = buffer;

            /* Macro repeats stop when the connection is lost.  */
            if (!Port.IsOpen)
                Repeater.StopAll();
        }

        /* Show the received data DATAIN.  Calls from the reading thread are
           passed on to the UI thread; data is held back while the welcome
           animation runs.  */
        public void DataReceive(string dataIn)
        {
            if (InvokeRequired)
            {
                PostToUi(new StringDelegate(DataReceive), dataIn);
                return;
            }

            /* Hold data back while the welcome animation owns the text
               box.  */
            if (WelcomeRunning)
            {
                PendingReceive.Append(dataIn);
                return;
            }

            AddReceived(dataIn);
        }

        /* Show RAWDATA, received data with one char per byte.  The hex view
           shows those exact bytes; the text view decodes them as UTF-8.  */
        private void AddReceived(string rawData)
        {
            byte[] bytes = Serial.RawEncoding.GetBytes(rawData);

            char[] chars = new char[ReceiveDecoder.GetCharCount(bytes, 0, bytes.Length)];
            ReceiveDecoder.GetChars(bytes, 0, bytes.Length, chars, 0);

            AddOutput(new string(chars), bytes, 1, true);
        }

        /* Queue a call of METHOD with DATA on the UI thread without waiting
           for it.  Waiting, as Invoke does, would block the reading thread and
           could deadlock against Serial.Close running on the UI thread.  Calls
           arriving while the window closes are dropped.  */
        private void PostToUi(StringDelegate method, string data)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke(method, data);
            }
            catch (InvalidOperationException)
            {
                /* The window handle was destroyed between the check and the
                   call.  */
            }
        }

        /* Timer handlers.  */

        /* Auto-connect timer tick: update the connection indicators, blink the
           auto-connect light and try to open the port if it is closed.  */
        private void ReconnectTick(object sender, EventArgs e)
        {
            ConnectButton.Text = Port.IsOpen ? "Disconnect" : "Connect";
            ConnectButton.ForeColor = Port.IsOpen ? Color.Red : Color.Green;
            SerialPictureBox.BackColor = Port.IsOpen ? Color.Green : Color.Transparent;
            AutoConnectPictureBox.BackColor = AutoConnectPictureBox.BackColor == Color.Transparent ? Color.Green : Color.Transparent;
            if (!Port.IsOpen) Port.Open();
        }

        /* Helper functions.  */

        /* Connect the Click events of the macro buttons, MacroButton0 and up,
           to MacroButton_Click.  */
        private void AssignButtonClickHandlers()
        {
            for (int i = 0; i < Settings.Layout.ButtonsPerPage; i++)
            {
                /* Find the button in the form.  */
                Button macroButton = (Button)this.Controls.Find($"MacroButton{i}", true).FirstOrDefault();
                if (macroButton != null)
                {
                    /* Copy the loop variable so that each handler gets its own
                       number.  */
                    int buffer = i;

                    /* Set click handler for this button.  */
                    macroButton.Click += (sender, e) => MacroButton_Click(buffer);
                }
            }
        }

        /* Show the page names on the radio buttons, and the labels of the
           current page's macros on the macro buttons.  While the macro
           buffer is open they show what is typed there, not yet saved.  */
        private void LoadGroupContent()
        {
            UpdateMacrosTitle();
            bool preview = MacroBufferVisible && MacroBuffer.Page == CurrentPage;

            for (int i = 0; i < Settings.Layout.PagesPerGroup; i++)
            {
                if (Controls.Find($"RadioButton{i}", true).FirstOrDefault() is RadioButton radioButton)
                    radioButton.Text = preview && PageIndex + i == CurrentPage
                        ? MacroBuffer.PreviewPageName
                        : Settings.Page.Text[i + PageIndex];
            }

            for (int i = 0; i < Settings.Layout.ButtonsPerPage; i++)
            {
                if (!(Controls.Find($"MacroButton{i}", true).FirstOrDefault() is Button macroButton))
                    continue;

                int macro = i + MacroBank + CurrentRadioValue;
                string label = Settings.MacroText.Text[macro] ?? "";
                string command = Settings.MacroCommand.Text[macro] ?? "";
                int interval = MacroRepeater.IntervalOf(macro);
                MacroState state = MacroState.Saved;
                if (preview)
                    MacroBuffer.Preview(i, out label, out command, out interval, out state);
                bool running = Repeater.IsRunning(macro);

                PaintMacroButton(macroButton, label, command, interval > 0, running, state);

                /* The tip shows the command, the repeat and the button's
                   shortcut.  */
                string key = i < 8 ? $"Ctrl+{i + 1}" : $"Ctrl+Shift+{i - 7}";
                string tip = command.Trim().Length > 0 ? command.Trim() : "Empty - right-click to edit";
                if (command.Trim().Length > 0 && interval > 0)
                    tip += running ? $"\nRepeating every {interval} ms - click to stop"
                                   : $"\nRepeats every {interval} ms - click to start";
                MacroTips.SetToolTip(macroButton, $"{tip}\n{key}");
            }
        }

        /* Give macro button BUTTON its LABEL, marked "~" if it REPEATS,
           "\u25BA" if COMMAND runs a script and "!" if it is risky.  A long
           label is shortened so that the mark stays visible.  A RUNNING
           repeat gets a green border; editing shows a border for STATE:
           amber when changed, red for a problem.

           The marks are characters that Consolas and the Windows system
           fonts all have, so they never show as empty boxes.  */
        private static void PaintMacroButton(Button button, string label, string command, bool repeats,
                                             bool running, MacroState state)
        {
            string text = command.Trim();
            string mark = repeats ? " ~"
                        : text.StartsWith(">load", StringComparison.Ordinal) ? " \u25BA"
                        : MacroBufferPane.IsRisky(text) ? " !"
                        : "";
            button.Text = FitLabel(button, label.Trim(), mark);

            if (running && state == MacroState.Saved)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = Color.FromArgb(0xDC, 0xFC, 0xE7);
                button.FlatAppearance.BorderSize = 2;
                button.FlatAppearance.BorderColor = Color.FromArgb(0x16, 0xA3, 0x4A);
                return;
            }

            if (state == MacroState.Saved)
            {
                button.FlatStyle = FlatStyle.Standard;
                button.BackColor = Color.Transparent;
                return;
            }

            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = SystemColors.Control;
            button.FlatAppearance.BorderSize = 2;
            button.FlatAppearance.BorderColor = state == MacroState.Problem
                ? Color.FromArgb(0xDC, 0x26, 0x26)
                : Color.FromArgb(0xF5, 0x9E, 0x0B);
        }

        /* Return LABEL followed by MARK, with the end of LABEL replaced by
           "\u2026" as far as needed to fit on BUTTON.  */
        private static string FitLabel(Button button, string label, string mark)
        {
            if (label.Length == 0)
                return "";
            int room = button.ClientSize.Width - 8;
            if (room <= 0 || TextRenderer.MeasureText(label + mark, button.Font).Width <= room)
                return label + mark;
            for (int length = label.Length - 1; length > 0; length--)
            {
                string shortened = label.Substring(0, length).TrimEnd() + "\u2026" + mark;
                if (TextRenderer.MeasureText(shortened, button.Font).Width <= room)
                    return shortened;
            }
            return label.Substring(0, 1) + "\u2026" + mark;
        }

        /*----------------------------------------------------------
         * Macro buffer
         *---------------------------------------------------------*/

        /* True while the macro buffer is shown.  */
        private bool MacroBufferVisible => MacroBuffer.Visible;

        /* Put the macro buffer, hidden, exactly over the output window, and
           connect its events.  The output window stays underneath and keeps
           receiving while the buffer is shown.  */
        private void CreateMacroBuffer()
        {
            MacroBuffer.Visible = false;
            MacroBuffer.Bounds = RichTextBox.Bounds;
            MacroBuffer.Anchor = RichTextBox.Anchor;
            Controls.Add(MacroBuffer);
            MacroBuffer.BringToFront();

            MacroBuffer.PageRequested += page => GoToPage(page);
            MacroBuffer.SendRequested += RunMacroCommand;
            MacroBuffer.CloseRequested += () => SetMacroBuffer(false);
            MacroBuffer.Edited += LoadGroupContent;
            MacroBuffer.Saved += () => { Repeater.Refresh(); LoadGroupContent(); };
            MacroBuffer.Moved += moved =>
            {
                /* Repeats follow their macros to new slots; repeats of
                   macros on other pages are not touched.  */
                int first = MacroBuffer.Page * Settings.Layout.ButtonsPerPage;
                Repeater.Move(slot =>
                    slot < first || slot >= first + Settings.Layout.ButtonsPerPage ? slot
                    : moved.TryGetValue(slot, out int target) ? target : -1);
            };
        }

        /* Show or hide the macro buffer over the output window as SHOW says.
           Showing loads the current page; hiding asks about unsaved changes
           and shows the output again, with everything received meanwhile.  */
        private void SetMacroBuffer(bool show)
        {
            if (show == MacroBufferVisible)
            {
                if (show)
                    MacroBuffer.Focus();
                return;
            }

            if (show)
            {
                /* One editor at a time over the output.  */
                if (HighlightPaneVisible)
                {
                    SetHighlightPane(false);
                    if (HighlightPaneVisible)
                        return;
                }
                FindBar.Visible = false;
                MacroBuffer.ShowPage(CurrentPage);
                MacroBuffer.ResetReceived();
                MacroBuffer.Bounds = RichTextBox.Bounds;
                MacroBuffer.Visible = true;
                MacroBuffer.BringToFront();
                MacroBuffer.FocusRow(0);
            }
            else
            {
                if (!MacroBuffer.ConfirmLeave())
                    return;

                MacroBuffer.Visible = false;
                RichTextBox.Focus();
                if (Settings.Output.Scroll)
                {
                    RichTextBox.SelectionStart = RichTextBox.TextLength;
                    RichTextBox.ScrollToCaret();
                }
            }

            MacroModeToolStripMenuItem.Checked = show;
            LoadGroupContent();
        }

        /* Add LINE to the output.  TYPE 0 is sent data (local echo), shown in
           hex in the hex view; TYPE 1 is a local message, always shown as
           text.  */
        private void AddLine(string line, int type)
        {
            if (type == 0)
                AddOutput(line, Encoding.UTF8.GetBytes(line), 0, true);
            else
                AddOutput(line, null, 1, false);
        }

        /* Append TEXT, or in the hex view a hex dump of BYTES, in the color
           for TYPE: 0 for sent, 1 for received.  ISDATA is true for data and
           false for local messages, which are never shown in hex.  Keep the
           log under its size limit and the user's selection in place, and
           blink the TX or RX light for data.  */
        private void AddOutput(string text, byte[] bytes, int type, bool isData)
        {
            LogText(text);

            /* The macro buffer covers the output: show it the new text.  */
            if (MacroBufferVisible)
                MacroBuffer.MarkOutput(text);

            int selectionStart = RichTextBox.SelectionStart;
            int selectionLength = RichTextBox.SelectionLength;
            bool caretAtEnd = selectionLength == 0 && selectionStart >= RichTextBox.TextLength;
            int removed;

            SetRedraw(false);
            try
            {
                if (isData && Settings.Output.HexOutput)
                {
                    AppendHex(bytes, type);
                    OutputLineEnded();
                }
                else
                {
                    EndHexLine();
                    AppendLines(text, type == 0 ? TransmitColor : ReceiveColor, true);
                }

                removed = TrimLog();
                OutputTrimmed(removed);
            }
            finally
            {
                SetRedraw(true);
            }

            /* Restore the user's selection, shifted by any trimmed text.  */
            if (!caretAtEnd)
                RestoreSelection(selectionStart - removed, selectionLength);

            if (isData)
            {
                /* Blink TX or RX indicator.  */
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
            }

            if (Settings.Output.Scroll)
            {
                RichTextBox.SelectionStart = RichTextBox.TextLength;
                RichTextBox.ScrollToCaret();
            }
        }

        /* Append TEXT in COLOR at the end of the output.  The color applies to
           the new text only, never to the user's selection, which is kept.  */
        private void AppendColored(string text, Color color, bool decorate = true)
        {
            if (MacroBufferVisible)
                MacroBuffer.MarkOutput(text);
            if (decorate)
                LogText(text);

            int selectionStart = RichTextBox.SelectionStart;
            int selectionLength = RichTextBox.SelectionLength;
            bool caretAtEnd = selectionLength == 0 && selectionStart >= RichTextBox.TextLength;

            EndHexLine();
            AppendLines(text, color, decorate);

            if (!caretAtEnd)
                RestoreSelection(selectionStart, selectionLength);
        }

        /* Select LENGTH characters from START, clamped to the current
           text.  */
        private void RestoreSelection(int start, int length)
        {
            if (start < 0)
            {
                length = Math.Max(0, length + start);
                start = 0;
            }

            start = Math.Min(start, RichTextBox.TextLength);
            length = Math.Min(length, RichTextBox.TextLength - start);

            RichTextBox.SelectionStart = start;
            RichTextBox.SelectionLength = length;
        }

        /* Show the animated ASCII-art welcome message, then clear it.  Data
           received meanwhile is shown afterwards.  */
        private async void WelcomeMsg()
        {
            if (WelcomeRunning)
                return;
            WelcomeRunning = true;

            RichTextBox.BackColor = Color.FromArgb(30, 30, 30);
            string archSuffix = Environment.Is64BitProcess ? " x64" : " x86";
            string version = About_Form.ShownVersion + archSuffix;
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
                $"                                                                     Version: VIBE CODED {version}"
            };
            /* Colors to cycle through.  */
            Color[] colors = new Color[]
            {
                Color.Firebrick,       /* Deep red.  */
                Color.DarkOrange,      /* Rich orange.  */
                Color.Gold,            /* Warm yellow.  */
                Color.MediumSeaGreen,  /* Muted green.  */
                Color.SteelBlue,       /* Soft blue-gray.  */
                Color.MediumSlateBlue, /* Cool purple-blue.  */
                Color.Tomato,          /* Bright red-orange.  */
                Color.MediumTurquoise, /* Vibrant teal.  */
                Color.SandyBrown,      /* Light brown-orange.  */
                Color.MediumVioletRed  /* Purple-pink.  */
            };
            RichTextBox.Clear();
            for (int i = 0; i < asciiLines.Length; i++)
            {
                AppendColored(asciiLines[i] + "\r\n", colors[i % colors.Length], false);
                await Task.Delay(100);
            }
            await Task.Delay(250);
            RichTextBox.Clear();
            RichTextBox.BackColor = BackgroundColor;

            /* Show anything that arrived during the animation.  */
            WelcomeRunning = false;
            if (PendingReceive.Length > 0)
            {
                string pending = PendingReceive.ToString();
                PendingReceive.Clear();
                AddReceived(pending);
            }
        }

        /* Show MESSAGE as an error in the output.  */
        private void ErrorMessage(string message)
        {
            AppendColored("[ Error ] " + message, TransmitColor);
        }

        /* Show MESSAGE as information in the output.  */
        private void InfoMessage(string message)
        {
            AppendColored("[ Info ] " + message, TransmitColor);
        }        

        /* Ignore right-clicks on the title bar and borders (the system menu)
           and double-clicks on the title bar (maximize); pass every other
           message BUFFER on.  */
        protected override void WndProc(ref Message buffer)
        {
            if (!IsBlockedNonClientMessage(buffer.Msg))
                base.WndProc(ref buffer);
        }

        /* Return true for the window messages that WndProc ignores:
           WM_NCLBUTTONDBLCLK, WM_NCRBUTTONDOWN, WM_NCRBUTTONUP and
           WM_NCRBUTTONDBLCLK.  */
        internal static bool IsBlockedNonClientMessage(int msg) =>
            msg == 0xA3 || msg == 0xA4 || msg == 0xA5 || msg == 0xA6;

        /* Apply the colors, font, word wrap and scrolling from the
           settings.  */
        private void ApplyDisplaySettings()
        {
            /* Colors.  */
            BackgroundColor = Color.FromArgb(Settings.Colors.Back.R, Settings.Colors.Back.G, Settings.Colors.Back.B);
            TransmitColor = Color.FromArgb(Settings.Colors.Transmit.R, Settings.Colors.Transmit.G, Settings.Colors.Transmit.B);
            ReceiveColor = Color.FromArgb(Settings.Colors.Receive.R, Settings.Colors.Receive.G, Settings.Colors.Receive.B);

            /* Font: an unknown family falls back to the system font, and a
               style the family lacks falls back to Regular.  */
            FontStyle style = Enum.TryParse(Settings.Output.FontStyle, out FontStyle parsedStyle)
                ? parsedStyle
                : FontStyle.Regular;

            if (Settings.Output.Underline)
                style |= FontStyle.Underline;

            if (Settings.Output.Strikeout)
                style |= FontStyle.Strikeout;

            try
            {
                RichTextBox.Font = new Font(Settings.Output.Font, Settings.Output.Size, style);
            }
            catch (ArgumentException)
            {
                RichTextBox.Font = new Font(Settings.Output.Font, Settings.Output.Size, FontStyle.Regular);
            }

            /* Word wrap and scrolling.  */
            RichTextBox.WordWrap = WordWrapToolStripMenuItem.Checked = Settings.Output.WordWrap;
            ToggleScrollingToolStripMenuItem.Checked = Settings.Output.Scroll;
            HexViewToolStripMenuItem.Checked = Settings.Output.HexOutput;

            /* Timestamps, highlights and the log.  */
            ApplyOutputToolSettings();

            /* The macro buffer uses the output font, at a readable size.  */
            MacroBuffer.SetEditorFont(RichTextBox.Font);
        }

        /* Append nothing to sent commands.  */
        private void NonePictureBox_Click(object sender, EventArgs e) =>
            SetAppend(Settings.Port.AppendType.AppendNothing);

        /* Append CR to sent commands.  */
        private void CRPictureBox_Click(object sender, EventArgs e) =>
            SetAppend(Settings.Port.AppendType.AppendCR);

        /* Append LF to sent commands.  */
        private void LFPictureBox_Click(object sender, EventArgs e) =>
            SetAppend(Settings.Port.AppendType.AppendLF);

        /* Append CR LF to sent commands.  */
        private void CRLFPictureBox_Click(object sender, EventArgs e) =>
            SetAppend(Settings.Port.AppendType.AppendCRLF);

        /* Append APPEND to sent commands from now on, show it and save the
           setting.  */
        private void SetAppend(Settings.Port.AppendType append)
        {
            Settings.Port.Append = append;
            UpdateAppendButtons();
            Settings.WriteXml();
        }

        /* Light the indicator of the line ending chosen in the settings.  */
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
