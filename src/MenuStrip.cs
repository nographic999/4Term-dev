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
         * [~] ScanCOMsToolStripMenuItem_Click - com ports description needs extra packages or works via suspicious method, needs changes
         * [✓] NewWindowToolStripMenuItem_Click
         * ---------------------------------------------------------
         * [~] ConnectDisconnectToolStripMenuItem_Click - needs testing
         * [~] AutoConnectToolStripMenuItem_Click - needs testing
         * ---------------------------------------------------------
         * [~] OpenToolStripMenuItem_Click - needs testing
         * ---------------------------------------------------------
         * [ ] 
         * ---------------------------------------------------------
         * [ ] ExitToolStripMenuItem_Click - Needs buffer cleaning and com port closing
         *---------------------------------------------------------*/

         /*----------------------------------------------------------
         * NewPortStripMenuItem_Click
         * 
         * Handles the "New Port" menu item click event.
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
         * Handles the "Port Options..." menu item click event.
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
         * Handles the "Scan COMs" menu item click event.
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
         * Handles the "New Window" menu item click event.
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
         * Handles the "Connect/Disconnect" menu item click event.
         *---------------------------------------------------------*/    
        private void ConnectDisconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Call the same logic used by the Connect button */
            ConnectButton_Click(sender, e);
        }

        /*----------------------------------------------------------
         * ConnectDisconnectToolStripMenuItem_Click
         * 
         * Handles the "Auto Connect" menu item click event.
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
         * Handles the "Open..." menu item click event.
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
         * OpenToolStripMenuItem_Click
         * 
         * Handles the "Open..." menu item click event.
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

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e) // nothing changed, must be changed
        {
            SaveAs();
        }

        /*----------------------------------------------------------
         * Separator
         *---------------------------------------------------------*/ 

        private void OpenConfiginiToolStripMenuItem1_Click(object sender, EventArgs e) // added messagebox, added port close before opening config
        {
            if (Port.IsOpen)
            {
                DialogResult result = MessageBox.Show("Do you want to close the serial port?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                    ConnectButton_Click(this, EventArgs.Empty);
                else
                    return;
            }
            RichTextBox.Clear();
            RichTextBox.Text = File.ReadAllText(Settings.XmlName);
            FilePath = Settings.XmlName;
        }

        /*----------------------------------------------------------
         * Separator
         *---------------------------------------------------------*/ 

        /*----------------------------------------------------------
         * ExitToolStripMenuItem_Click
         * 
         * Handles the "Exit" menu item click event.
         *---------------------------------------------------------*/
        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Needs buffer cleaning and com port closing*/
            Close();
        }

        ////////////////////////////////////////////////////////////////////////////////
        //                              MenuStrip
        ////////////////////////////////////////////////////////////////////////////////
        //      // Edit //

        private void UndoToolStripMenuItem_Click(object sender, EventArgs e) // nothing changed
        {
            RichTextBox.Undo();
        }

        ////////////////////////////////////////////////////////////////////////////////
        //                              Separator
        ////////////////////////////////////////////////////////////////////////////////

        private void CutToolStripMenuItem_Click(object sender, EventArgs e) // nothing changed
        {
            RichTextBox.Cut();
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e) // nothing changed
        {
            RichTextBox.Copy();
        }

        private void PasteToolStripMenuItem_Click(object sender, EventArgs e) // nothing changed
        {
            RichTextBox.Paste();
        }

        private void DeleteToolStripMenuItem_Click(object sender, EventArgs e) // nothing changed
        {
            RichTextBox.SelectedText = "";
        }

        ////////////////////////////////////////////////////////////////////////////////
        //                              Separator
        ////////////////////////////////////////////////////////////////////////////////

        private void SelectAllToolStripMenuItem_Click(object sender, EventArgs e) // nothing changed
        {
            RichTextBox.SelectAll();
        }

        ////////////////////////////////////////////////////////////////////////////////
        //                              Separator
        ////////////////////////////////////////////////////////////////////////////////

        private void ClearToolStripMenuItem_Click(object sender, EventArgs e) // nothing changed
        {
            RichTextBox.Clear();
        }

        ////////////////////////////////////////////////////////////////////////////////
        //                              MenuStrip
        ////////////////////////////////////////////////////////////////////////////////
        //              // Format //

        private void WordWrapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RichTextBox.WordWrap = !RichTextBox.WordWrap;
            Settings.RichTextBox.WordWrap = WordWrapToolStripMenuItem.Checked = RichTextBox.WordWrap;
            Settings.WriteXml();
        }

        private void SettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings_Form SettingsForm = new Settings_Form();
            SettingsForm.ShowDialog();
        }

        ////////////////////////////////////////////////////////////////////////////////
        //                              MenuStrip
        ////////////////////////////////////////////////////////////////////////////////
        //                        // View //

        private void ToggleScrollingToolStripMenuItem_Click(object sender, EventArgs e) // added save after click
        {
            ToggleScrollingToolStripMenuItem.Checked = !ToggleScrollingToolStripMenuItem.Checked;
            Settings.RichTextBox.Scroll = ToggleScrollingToolStripMenuItem.Checked;
            Settings.WriteXml();
        }

        ////////////////////////////////////////////////////////////////////////////////
        //                              Separator
        ////////////////////////////////////////////////////////////////////////////////

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

        ////////////////////////////////////////////////////////////////////////////////
        //                              MenuStrip
        ////////////////////////////////////////////////////////////////////////////////
        //                                // Help //

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About_Form AboutForm = new About_Form();
            AboutForm.ShowDialog();
        }

        ////////////////////////////////////////////////////////////////////////////////
        //                          Helper Functions
        ////////////////////////////////////////////////////////////////////////////////

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
