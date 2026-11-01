/* MenuStrip.cs -- handlers for the main window's menus: File, Edit, Format,
   View and Help.  Part of the Main_Form class.

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Windows.Forms;

namespace _4Term
{
    public partial class Main_Form : Form
    {
        /* File menu.  */

        /* Configure a new connection: close the open port after asking, show
           the serial options dialog, then clear the output and open the port.
           Do nothing if the user keeps the connection or cancels the
           dialog.  */
        private void NewPortStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Ask to close an open port; stop if the user keeps it.  */
            if (!ConfirmClosePort(NewConnectionQuestion, MessageBoxDefaultButton.Button1))
                return;

            /* Open the SerialPortOptionsForm dialog; do nothing if
               cancelled.  */
            if (!ShowSerialOptions())
                return;

            /* Clear the output.  */
            RichTextBox.Clear();

            /* Open the serial port.  */
            Port.Open();
        }

        /* Show the serial options dialog.  */
        private void PortOptionsToolStripMenuItem_Click(object sender, EventArgs e) => ShowSerialOptions();

        /* Show the serial options dialog.  When the user saves, update the
           DTR, RTS and line ending indicators; if the port is open, apply DTR
           and RTS at once and note that the other settings take effect on
           reconnect.  Return true if the settings were saved.  */
        private bool ShowSerialOptions()
        {
            using (SerialPortOptionsForm OptionsForm = new SerialPortOptionsForm())
            {
                if (OptionsForm.ShowDialog() != DialogResult.OK)
                    return false;
            }

            UpdateLineIndicators();
            UpdateAppendButtons();

            if (Port.IsOpen)
            {
                Port.SetDtr(Settings.Port.Dtr);
                Port.SetRts(Settings.Port.Rts);
                InfoMessage("Port settings saved. Reconnect to apply port, baud rate and framing changes.\n");
            }

            return true;
        }

        /* List the available COM ports in the output window, with the device
           description from WMI where available.  */
        private void ScanCOMsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            const string Separator = "-------------------------------------------------------------------------------------------------\n";

            AppendColored(Separator, TransmitColor);

            /* Get the port names, such as "COM1" and "COM3".  */
            string[] allPorts = SerialPort.GetPortNames();

            /* Check if any ports were found.  */
            if (allPorts.Length == 0)
            {
                AppendColored("No COM ports found\n", Color.Red);
                AppendColored(Separator, TransmitColor);
                return;
            }

            AppendColored("Detected COM Ports:\n", TransmitColor);

            /* Get the device descriptions from WMI; they are easier to
               recognize than bare port names.  */
            Dictionary<string, string> portDescriptions = new Dictionary<string, string>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Caption, DeviceID FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'"))
                {
                    /* Execute query and process results.  */
                    foreach (ManagementBaseObject baseObj in searcher.Get())
                    {
                        ManagementObject obj = (ManagementObject)baseObj;
                        /* Get the device caption (description) and ID.  */
                        string caption = obj["Caption"]?.ToString();
                        string deviceId = obj["DeviceID"]?.ToString();

                        /* Skip entries without a caption or ID.  */
                        if (caption != null && deviceId != null)
                        {
                            /* Extract the port name from the caption.  */
                            var match = System.Text.RegularExpressions.Regex.Match(caption, @"\((COM\d+)\)");
                            if (match.Success)
                            {
                                string comPort = match.Groups[1].Value;
                                /* Clean up the description by removing the COM
                                   port reference.  */
                                string description = caption.Replace($"({comPort})", "").Trim();
                                /* Store in dictionary for later lookup.  */
                                portDescriptions[comPort] = description;
                            }
                        }
                    }
                }
            }
            /* The WMI query can fail because of permissions or other
               issues.  */
            catch (Exception ex)
            {
                AppendColored($"Note: Could not retrieve detailed port info ({ex.Message})\n", TransmitColor);
            }

            /* Show the ports in alphabetical order, with their
               descriptions.  */
            foreach (string port in allPorts.OrderBy(p => p))
            {
                AppendColored($"{port}: ", Color.Tomato);

                /* Use the WMI description if there is one.  */
                if (portDescriptions.TryGetValue(port, out string description))
                    AppendColored($"{description}\n", Color.MediumTurquoise);
                else
                    AppendColored("[Driver/Device description not available]\n", Color.Tomato);
            }

            /* Show the total.  */
            AppendColored(Separator, TransmitColor);
            AppendColored("Total ports detected: ", TransmitColor);
            AppendColored($"{allPorts.Length}\n", Color.Gold);
        }

        /* Start another copy of 4Term.  */
        private void NewWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(Application.ExecutablePath);
            }
            catch (Exception ex)
            {
                ErrorMessage($"Could not open a new window: {ex.Message}\n");
            }
        }

        /* Connect or disconnect, like the Connect button.  */
        private void ConnectDisconnectToolStripMenuItem_Click(object sender, EventArgs e) => ConnectButton_Click(sender, e);

        /* Switch auto-connect on or off, like the AutoConnect button.  */
        private void AutoConnectToolStripMenuItem_Click(object sender, EventArgs e) => AutoConnectButton_Click(sender, e);

        /* Load a text or log file into the output window, after asking to
           close the port if it is open.  Files over 10 MB are refused.  */
        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            { 
                /* Ask to close an open port; stop if the user keeps it.  */
                if (!ConfirmClosePort(FileQuestion, MessageBoxDefaultButton.Button2))
                    return;
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    /* Set up the dialog.  */
                    openFileDialog.Title = "Select File to Open";
                    openFileDialog.Filter = "Log Files (*.log)|*.log|Config Files (*.cfg)|*.cfg|All Files (*.*)|*.*";
                    openFileDialog.FilterIndex = 1;
                    openFileDialog.RestoreDirectory = true;
                    openFileDialog.CheckFileExists = true;
                    openFileDialog.CheckPathExists = true;

                    /* Process file selection.  */
                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                    
                            /* Remember the file for File > Save.  */
                            FilePath = openFileDialog.FileName;

                            /* Check the file size before loading.  */
                            FileInfo fileInfo = new FileInfo(FilePath);
                            /* Refuse files over 10 MB.  */
                            if (fileInfo.Length > 10 * 1024 * 1024)
                            {
                                MessageBox.Show(
                                    "The selected file is too large (maximum 10MB allowed).",
                                    "File Size Limit Exceeded",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                                return;
                            }

                            /* Clear existing content.  */
                            RichTextBox.Clear();

                            /* Read the file as UTF-8.  */
                            RichTextBox.Text = File.ReadAllText(FilePath, Encoding.UTF8);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}",
                              "Error",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Error);
            }
        }

        /* Save the output to the current file, or ask for a file name if there
           is none.  Ask to close the port first if it is open.

           FIXME: the message boxes here were added as a stopgap and are to be
           changed.  */
        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Ask to close an open port; stop if the user keeps it.  */
            if (!ConfirmClosePort(FileQuestion, MessageBoxDefaultButton.Button2))
                return;

            /* Refuse to save empty output.  */
            if (string.IsNullOrWhiteSpace(RichTextBox.Text))
            {
                MessageBox.Show("There is nothing to save.",
                              "Warning",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Warning);
                return;
            }

            /* Save to the current file, or ask for a new one.  */
            try
            {
                if (!string.IsNullOrWhiteSpace(FilePath))
                    SaveTextTo(FilePath);
                else
                    SaveAsToolStripMenuItem_Click(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}",
                              "Error",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Error);
            }
        }

        /* Ask for a file name and save the output there.  Ask to close the
           port first if it is open.  */
        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Ask to close an open port; stop if the user keeps it.  */
            if (!ConfirmClosePort(FileQuestion, MessageBoxDefaultButton.Button2))
                return;

            /* Refuse to save empty output.  */
            if (string.IsNullOrWhiteSpace(RichTextBox.Text))
            {
                MessageBox.Show("There is nothing to save.",
                              "Warning",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Warning);
                return;
            }

            /* Show save dialog and save file.  */
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Log Files (*.log)|*.log|Config Files (*.cfg)|*.cfg|Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                saveFileDialog.FilterIndex = 1;
                saveFileDialog.OverwritePrompt = true;
                saveFileDialog.RestoreDirectory = true;

                /* Start in the folder of the current file, if there is
                   one.  */
                if (!string.IsNullOrWhiteSpace(FilePath))
                {
                    saveFileDialog.InitialDirectory = Path.GetDirectoryName(FilePath);
                    saveFileDialog.FileName = Path.GetFileNameWithoutExtension(FilePath);
                }

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (SaveTextTo(saveFileDialog.FileName))
                        {
                            FilePath = saveFileDialog.FileName;
                            MessageBox.Show("File saved successfully.",
                                          "Success",
                                          MessageBoxButtons.OK,
                                          MessageBoxIcon.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error: {ex.Message}",
                                      "Error",
                                      MessageBoxButtons.OK,
                                      MessageBoxIcon.Error);
                    }
                }
            }
        }

        /* Show config.xml in the output window for editing, after asking to
           close the port if it is open.  Saving it with File > Save checks the
           XML and reloads the settings.  */
        private void OpenConfigXmlToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                /* Ask to close an open port; stop if the user keeps it.  */
                if (!ConfirmClosePort(FileQuestion, MessageBoxDefaultButton.Button1))
                    return;

                /* Check that the file exists.  */
                if (!File.Exists(Xml.FileName))
                {
                    MessageBox.Show($"Configuration file not found: {Xml.FileName}",
                                  "File Missing",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Warning);
                    return;
                }

                /* Refuse files over 1 MB.  */
                FileInfo configFile = new FileInfo(Xml.FileName);
                if (configFile.Length > 1024 * 1024)
                {
                    MessageBox.Show("Configuration file exceeds maximum size (1MB)",
                                  "File Too Large",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Error);
                    return;
                }

                /* Clear and load content.  */
                RichTextBox.Clear();

                /* Read the file as UTF-8.  */
                RichTextBox.Text = File.ReadAllText(Xml.FileName, Encoding.UTF8);
                FilePath = Xml.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}",
                              "Error",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Error);
            }
        }

        /* Close the main window.  Closing it stops the macro timers and closes
           the port.  */
        private void ExitToolStripMenuItem_Click(object sender, EventArgs e) => this.Close();

        /* Edit menu.  */

        /* Undo the last change in the output window.  */
        private void UndoToolStripMenuItem_Click(object sender, EventArgs e) => RichTextBox.Undo();

        /* Cut the selected text to the clipboard.  */
        private void CutToolStripMenuItem_Click(object sender, EventArgs e) => RichTextBox.Cut();

        /* Copy the selected text to the clipboard.  */
        private void CopyToolStripMenuItem_Click(object sender, EventArgs e) => RichTextBox.Copy();

        /* Paste the clipboard at the cursor.  */
        private void PasteToolStripMenuItem_Click(object sender, EventArgs e) => RichTextBox.Paste();
            
        /* Delete the selected text.  */
        private void DeleteToolStripMenuItem_Click(object sender, EventArgs e) => RichTextBox.SelectedText = "";

        /* Select all text in the output window.  */
        private void SelectAllToolStripMenuItem_Click(object sender, EventArgs e) => RichTextBox.SelectAll();

        /* Clear the output window.  */
        private void ClearToolStripMenuItem_Click(object sender, EventArgs e) => RichTextBox.Clear();

        /* Format menu.  */

        /* Switch word wrap on or off and save the setting.  */
        private void WordWrapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RichTextBox.WordWrap = !RichTextBox.WordWrap;
            Settings.Output.WordWrap = WordWrapToolStripMenuItem.Checked = RichTextBox.WordWrap;
            Settings.WriteXml();
        }

        /* Show the settings dialog and, if the user saved, apply the new
           colors, font and display options.  */
        private void SettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool highlights;
            using (Settings_Form SettingsForm = new Settings_Form())
            {
                if (SettingsForm.ShowDialog() != DialogResult.OK)
                    return;
                highlights = SettingsForm.HighlightsRequested;
            }

            /* Apply colors, font, word wrap, scrolling, timestamps and the
               log.  */
            ApplyDisplaySettings();

            /* Apply the background color.  */
            RichTextBox.BackColor = BackgroundColor;

            if (highlights)
                SetHighlightPane(true);
        }

        /* View menu.  */

        /* Switch automatic scrolling to new output on or off and save the
           setting.  */
        private void ToggleScrollingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleScrollingToolStripMenuItem.Checked = !ToggleScrollingToolStripMenuItem.Checked;
            Settings.Output.Scroll = ToggleScrollingToolStripMenuItem.Checked;
            Settings.WriteXml();
        }

        /* Show or hide the macro buffer below the output window.  */
        private void MacroModeToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMacroBuffer(!MacroBufferVisible);

        /* Help menu.  */

        /* Show the About dialog.  */
        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (About_Form AboutForm = new About_Form())
                AboutForm.ShowDialog();
        }

        /* Helper functions.  */

        /* Questions asked before closing an open port.  */
        private const string NewConnectionQuestion =
            "A serial port connection is currently active. Close it to configure a new one?";
        private const string FileQuestion =
            "A serial port connection is currently active. Close it to proceed with file operations?";

        /* If the port is open, ask QUESTION, with DEFAULTBUTTON preselected,
           and close the port if the user answers Yes.  Return false if the
           user wants to keep the port open.  */
        private bool ConfirmClosePort(string question, MessageBoxDefaultButton defaultButton)
        {
            if (!Port.IsOpen)
                return true;

            DialogResult result = MessageBox.Show(question,
                                                  "Active Serial Connection",
                                                  MessageBoxButtons.YesNo,
                                                  MessageBoxIcon.Question,
                                                  defaultButton);
            if (result == DialogResult.Yes)
                Port.Close();

            return result != DialogResult.No;
        }

        /* Write the output text to PATH and return true if the file was
           written.  When PATH is the configuration file, the text must be
           valid XML with a <Settings> root, so that terminal output cannot
           overwrite it, and the settings are reloaded afterwards, so that
           later saves do not undo the edit.  */
        private bool SaveTextTo(string path)
        {
            bool isConfig = string.Equals(Path.GetFullPath(path), Path.GetFullPath(Xml.FileName),
                StringComparison.OrdinalIgnoreCase);

            if (isConfig)
            {
                try
                {
                    var check = new System.Xml.XmlDocument();
                    check.LoadXml(RichTextBox.Text);
                    if (check.DocumentElement == null || check.DocumentElement.Name != "Settings")
                        throw new System.Xml.XmlException("The root element must be <Settings>.");
                }
                catch (System.Xml.XmlException ex)
                {
                    MessageBox.Show("The text is not a valid 4Term configuration, so it was not saved " +
                                    $"over {Path.GetFileName(path)}.\n\n{ex.Message}\n\nUse Save As to save it as another file.",
                                    "Not Saved",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                    return false;
                }
            }

            File.WriteAllText(path, RichTextBox.Text, Encoding.UTF8);

            if (isConfig)
            {
                Settings.ReadXml();
                ApplyDisplaySettings();
                RichTextBox.BackColor = BackgroundColor;
                UpdateLineIndicators();
                UpdateAppendButtons();
                if (MacroBufferVisible)
                    MacroBuffer.ShowPage(CurrentPage);
                LoadGroupContent();
            }

            return true;
        }

    }
}
