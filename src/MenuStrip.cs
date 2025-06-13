using System;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace _4Term
{
    public partial class Main_Form : Form
    {
        /*----------------------------------------------------------
         * MenuStrip - File
         * ---------------------------------------------------------
         * DEVELOPMENT STATUS
         *
         * [✓] NewPortStripMenuItem_Click
         * [✓] PortOptionsToolStripMenuItem_Click
         * [~] ScanCOMsToolStripMenuItem_Click
         * [✓] NewWindowToolStripMenuItem_Click
         * ---------------------------------------------------------
         * [✓] ConnectDisconnectToolStripMenuItem_Click
         * [✓] AutoConnectToolStripMenuItem_Click
         * ---------------------------------------------------------
         * [✓] OpenToolStripMenuItem_Click
         * [ ] SaveToolStripMenuItem_Click
         * [ ] SaveAsToolStripMenuItem_Click
         * ---------------------------------------------------------
         * [✓] OpenConfigXmlToolStripMenuItem_Click
         * ---------------------------------------------------------
         * [ ] ExitToolStripMenuItem_Click
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * NewPortStripMenuItem_Click
         * 
         * Closes the current serial port if open, clears the RichTextBox,
         * opens the SerialPortOptionsForm for new configuration,
         * then opens the serial port with updated settings.
         *---------------------------------------------------------*/
        private void NewPortStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Close the serial port if open */
            if (Port.IsOpen)
                Port.Close();
            
            /* Clear RichTextBox */
            RichTextBox.Clear();

            /* Open the SerialPortOptionsForm dialog to configure settings */
            SerialPortOptionsForm OptionsForm = new SerialPortOptionsForm();
            OptionsForm.ShowDialog();

            /* Open the serial port */
            Port.Open();
        }

        /*----------------------------------------------------------
         * PortOptionsToolStripMenuItem_Click
         * 
         * Opens the SerialPortOptionsForm dialog to configure serial port settings.
         *---------------------------------------------------------*/
        private void PortOptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Open the SerialPortOptionsForm dialog to configure settings */
            SerialPortOptionsForm OptionsForm = new SerialPortOptionsForm();
            OptionsForm.ShowDialog();
        }

        /*----------------------------------------------------------
         * ScanCOMsToolStripMenuItem_Click
         * 
         * Retrieves and displays the list of available COM ports 
         * in the RichTextBox.
         *---------------------------------------------------------*/
        private void ScanCOMsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Get an array of available serial port names */
            string[] portList = System.IO.Ports.SerialPort.GetPortNames();

            /* Append text to RichTextBox */
            RichTextBox.AppendText("----------------\nFound COM Ports:\n");
            foreach (string port in portList)
            {
                RichTextBox.AppendText($"{port}\n");
            }
            RichTextBox.AppendText("----------------\n");
        }

        /*----------------------------------------------------------
         * NewWindowToolStripMenuItem_Click
         * 
         * Launches a new instance of the current application.
         *---------------------------------------------------------*/
        private void NewWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Start new 4Term window */
            Process.Start(Process.GetCurrentProcess().ProcessName);
        }

        /*----------------------------------------------------------
         * Separator
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * ConnectDisconnectToolStripMenuItem_Click
         * 
         * Toggles the connection state by invoking the same logic 
         * as the Connect button.
         *---------------------------------------------------------*/
        private void ConnectDisconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Call the same logic used by the Connect button */
            ConnectButton_Click(sender, e);
        }

        /*----------------------------------------------------------
         * ConnectDisconnectToolStripMenuItem_Click
         * 
         * Toggles the Auto Connect feature by invoking the same logic 
         * as the Auto Connect button.
         *---------------------------------------------------------*/
        private void AutoConnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Call the same logic used by the Auto Connect button */
            AutoConnectButton_Click(sender, e);
        }

        /*----------------------------------------------------------
         * Separator
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * OpenToolStripMenuItem_Click
         * 
         * Opens a file dialog to select and load a file into the application.
         *---------------------------------------------------------*/
        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                /* Set file type filters for the dialog (.log, .cfg, all files) */
                openFileDialog.Filter = "Log Files (*.log)|*.log|Config Files (*.cfg)|*.cfg|All Files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;

                /* Open a file dialog for the user to select file */
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    /* Set file path */
                    FilePath = openFileDialog.FileName;

                    /* Null and file existence check */
                    if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) return;
                    
                    /* Clears the RichTextBox */
                    RichTextBox.Clear();

                    /* Load the selected file's content into the RichTextBox */
                    RichTextBox.Text = File.ReadAllText(FilePath);
                }
            }
        }

        /*----------------------------------------------------------
         * SaveToolStripMenuItem_Click
         * 
         * Saves the current document.
         *---------------------------------------------------------*/
        private void SaveToolStripMenuItem_Click(object sender, EventArgs e) // added messageboxes, must be changed
        {
            if (Port.IsOpen)
            {
                MessageBox.Show("Please close the serial port before saving the file.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(RichTextBox.Text))
            {
                MessageBox.Show("There is nothing to save.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!string.IsNullOrWhiteSpace(FilePath))
                File.WriteAllText(FilePath, RichTextBox.Text);
            else
                SaveAs();
        }

        /*----------------------------------------------------------
         * SaveAsToolStripMenuItem_Click
         * 
         * Saves the current document or content to a user-specified file.
         *---------------------------------------------------------*/
        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveAs();
        }

        /*----------------------------------------------------------
         * Separator
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * OpenConfigXmlToolStripMenuItem_Click
         * 
         * Opens and loads the configuration from the config.xml file.
         *---------------------------------------------------------*/
        private void OpenConfigXmlToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Check if the serial port is open; if so, ask the user if they want to close it */
            if (Port.IsOpen)
            {                
                DialogResult result = MessageBox.Show("Do you want to close the serial port?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                    Port.Close();
                else
                    return;
            }

            /* Clear RichTextBox */
            RichTextBox.Clear();

            /* Load all text from file to RichTextBox */
            RichTextBox.Text = File.ReadAllText(Settings.XmlName);
            FilePath = Settings.XmlName;
        }

        /*----------------------------------------------------------
         * Separator
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * ExitToolStripMenuItem_Click
         * 
         * Cleans buffers, closes the COM port properly, 
         * and exits the application.
         *---------------------------------------------------------*/
        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Needs buffer cleaning and com port closing*/
            //Close();
        }

        /*----------------------------------------------------------
         * MenuStrip - Edit
         *---------------------------------------------------------
         * DEVELOPMENT STATUS
         *
         * [✓] UndoToolStripMenuItem_Click
         * ---------------------------------------------------------
         * [✓] CutToolStripMenuItem_Click
         * [✓] CopyToolStripMenuItem_Click
         * [✓] PasteToolStripMenuItem_Click
         * [✓] DeleteToolStripMenuItem_Click
         * ---------------------------------------------------------
         * [✓] SelectAllToolStripMenuItem_Click
         * ---------------------------------------------------------
         * [✓] ClearToolStripMenuItem_Click
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * UndoToolStripMenuItem_Click
         * 
         * Reverts the last change made in the RichTextBox
         *---------------------------------------------------------*/
        private void UndoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RichTextBox.Undo();
        }

        /*----------------------------------------------------------
         * Separator
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * CutToolStripMenuItem_Click
         * 
         * Cuts the selected text from the RichTextBox
         * and places it on the clipboard
         *---------------------------------------------------------*/
        private void CutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RichTextBox.Cut();
        }

        /*----------------------------------------------------------
         * CopyToolStripMenuItem_Click
         * 
         * Copies the selected text from the RichTextBox
         * to the clipboard
         *---------------------------------------------------------*/
        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RichTextBox.Copy();
        }

        /*----------------------------------------------------------
         * PasteToolStripMenuItem_Click
         * 
         * Pastes the clipboard content into the RichTextBox
         * at the current cursor position
         *---------------------------------------------------------*/
        private void PasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RichTextBox.Paste();
        }

        /*----------------------------------------------------------
         * DeleteToolStripMenuItem_Click
         * 
         * Deletes the selected text from the RichTextBox
         *---------------------------------------------------------*/
        private void DeleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RichTextBox.SelectedText = "";
        }

        /*----------------------------------------------------------
         * Separator
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * SelectAllToolStripMenuItem_Click
         * 
         * Selects all the text in the RichTextBox
         *---------------------------------------------------------*/
        private void SelectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RichTextBox.SelectAll();
        }

        /*----------------------------------------------------------
         * Separator
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * ClearToolStripMenuItem_Click
         * 
         * Clears all text from the RichTextBox
         *---------------------------------------------------------*/
        private void ClearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RichTextBox.Clear();
        }

        /*----------------------------------------------------------
         * MenuStrip - Format
         *---------------------------------------------------------
         * DEVELOPMENT STATUS
         *
         * [✓] WordWrapToolStripMenuItem_Click
         * [✓] SettingsToolStripMenuItem_Click
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * WordWrapToolStripMenuItem_Click
         * 
         * Toggles word wrap in the RichTextBox. Updates the
         * settings and saves them to XML.
         *---------------------------------------------------------*/
        private void WordWrapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RichTextBox.WordWrap = !RichTextBox.WordWrap;
            Settings.RichTextBox.WordWrap = WordWrapToolStripMenuItem.Checked = RichTextBox.WordWrap;
            Settings.WriteXml();
        }

        /*----------------------------------------------------------
         * SettingsToolStripMenuItem_Click
         * 
         * Opens the Settings form as a modal dialog
         *---------------------------------------------------------*/
        private void SettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings_Form SettingsForm = new Settings_Form();
            SettingsForm.ShowDialog();
        }

        /*----------------------------------------------------------
         * MenuStrip - View
         *---------------------------------------------------------
         * DEVELOPMENT STATUS
         *
         * [✓] ToggleScrollingToolStripMenuItem_Click
         * [✓] MacroModeToolStripMenuItem_Click
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * ToggleScrollingToolStripMenuItem_Click
         * 
         * Toggles the custom scrolling feature. Updates the setting
         * and saves it to XML.
         *---------------------------------------------------------*/
        private void ToggleScrollingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleScrollingToolStripMenuItem.Checked = !ToggleScrollingToolStripMenuItem.Checked;
            Settings.RichTextBox.Scroll = ToggleScrollingToolStripMenuItem.Checked;
            Settings.WriteXml();
        }

        /*----------------------------------------------------------
         * Separator
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * MacroModeToolStripMenuItem_Click
         * 
         * Toggles Macro Mode. When enabled, it activates all macro 
         * editing UI elements and disables the main interface. 
         * Updates visibility and enable states accordingly.
         *---------------------------------------------------------*/
        private void MacroModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MacroModeToolStripMenuItem.Checked = !MacroModeToolStripMenuItem.Checked;
            MacroEditingFlag = !MacroEditingFlag;
            ProgButton.Enabled = ProgButton.Visible = MacroBox.Enabled = MacroBox.Visible =
                MacroTextBox.Enabled = MacroTextBox.Visible = MacroLabel.Enabled = MacroLabel.Visible =
                MacroTextLabel.Enabled = MacroTextLabel.Visible = TabLabel.Enabled = TabLabel.Visible =
                TabBox.Enabled = TabBox.Visible = labelinfo.Enabled = labelinfo.Visible = MacroEditingFlag;
            ComboBox.Enabled = ComboBox.Visible = SendButton.Enabled = SendButton.Visible =
               ClearButton.Enabled = ClearButton.Visible = SettingsButton.Enabled = SettingsButton.Visible =
               AutoConnectButton.Enabled = AutoConnectButton.Visible = ConnectButton.Enabled = ConnectButton.Visible =
               RichTextBox.Visible = PasteToolStripMenuItem.Enabled = CopyToolStripMenuItem.Enabled = CutToolStripMenuItem.Enabled =
               groupBox1.Enabled = groupBox1.Visible = COMBox.Enabled = COMBox.Visible = !MacroEditingFlag;
        }

        /*----------------------------------------------------------
         * MenuStrip - Help
         *---------------------------------------------------------
         * DEVELOPMENT STATUS
         *
         * [✓] AboutToolStripMenuItem_Click
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * AboutToolStripMenuItem_Click
         * 
         * Opens the About form as a modal dialog to display
         * application information.
         *---------------------------------------------------------*/
        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About_Form AboutForm = new About_Form();
            AboutForm.ShowDialog();
        }

        /*----------------------------------------------------------
         * Helper functions
         *---------------------------------------------------------
         * DEVELOPMENT STATUS
         *
         * [✓] SaveAs
         *---------------------------------------------------------*/

        /*----------------------------------------------------------
         * SaveAs
         * 
         * Saves the content of the RichTextBox to a file with 
         * extensions .log or .cfg (removed .txt). 
         * Displays message boxes for empty content or if the serial 
         * port is open (must be closed before saving).
         *---------------------------------------------------------*/
        private void SaveAs() // removed .txt added .log and .cfg extensions, added messageboxes
        {
            if (!Port.IsOpen)
            {
                if (string.IsNullOrWhiteSpace(RichTextBox.Text))
                {
                    MessageBox.Show("There is nothing to save.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "Log Files (*.log)|*.log|Config Files (*.cfg)|*.cfg|All Files (*.*)|*.*";
                    saveFileDialog.FilterIndex = 1;

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        FilePath = saveFileDialog.FileName;
                        File.WriteAllText(FilePath, RichTextBox.Text);
                    }
                }
            }
            else
                MessageBox.Show("Please close the serial port before saving the file.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
