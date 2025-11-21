using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Windows.Forms;

namespace _4Term
{
    public partial class SerialPortOptionsForm : Form
    {
        /*----------------------------------------------------------
         * Class variables
         * -------------------------------------------------------*/

        private Timer comPortMonitorTimer;
        private string[] lastKnownPorts = Array.Empty<string>();

        /*----------------------------------------------------------
         * Class Functions
         * -------------------------------------------------------*/

        /*----------------------------------------------------------
         * SerialPortOptionsForm (Constructor)
         * 
         * Initializes serial port configuration form components and loads 
         * saved settings into the UI controls.
        *---------------------------------------------------------*/
        public SerialPortOptionsForm()
        {
            InitializeComponent();
            comPortMonitorTimer = new Timer();
            comPortMonitorTimer.Interval = 250;
            comPortMonitorTimer.Tick += ComPortMonitorTimer_Tick;

            /* Populate COM port list */
            COMPortComboBox_Click(this, EventArgs.Empty);

            /* Load saved baud rate setting */
            switch (Settings.Port.BaudRate)
            {
                case 600: BaudRate600RadioButton.Checked = true; break;
                case 1200: BaudRate1200RadioButton.Checked = true; break;
                case 2400: BaudRate2400RadioButton.Checked = true; break;
                case 4800: BaudRate4800RadioButton.Checked = true; break;
                case 9600: BaudRate9600RadioButton.Checked = true; break;
                case 14400: BaudRate14400RadioButton.Checked = true; break;
                case 19200: BaudRate19200RadioButton.Checked = true; break;
                case 28800: BaudRate28800RadioButton.Checked = true; break;
                case 38400: BaudRate38400RadioButton.Checked = true; break;
                case 56000: BaudRate56000RadioButton.Checked = true; break;
                case 57600: BaudRate57600RadioButton.Checked = true; break;
                case 115200: BaudRate115200RadioButton.Checked = true; break;
                case 128000: BaudRate128000RadioButton.Checked = true; break;
                case 256000: BaudRate256000RadioButton.Checked = true; break;
                default:
                    /* Fallback to custom baud rate */
                    BaudRateCustomRadioButton.Checked = true;
                    BaudRateCustomRadioButton_Click(this, EventArgs.Empty);
                    CustomBRTextBox.Text = Settings.Port.BaudRate.ToString();
                    break;
            }

            /* Load saved append settings */
            switch (Settings.Port.Append)
            {
                case Settings.Port.AppendType.AppendNothing: AppendNoneRadioButton.Checked = true; break;
                case Settings.Port.AppendType.AppendCR: AppendCRRadioButton.Checked = true; break;
                case Settings.Port.AppendType.AppendLF: AppendLFRadioButton.Checked = true; break;
                case Settings.Port.AppendType.AppendCRLF: AppendCRLFRadioButton.Checked = true; break;
            }

            /* Load saved data bits */
            switch (Settings.Port.DataBits)
            {
                case 5: DataBits5RadioButton.Checked = true; break;
                case 6: DataBits6RadioButton.Checked = true; break;
                case 7: DataBits7RadioButton.Checked = true; break;
                case 8: DataBits8RadioButton.Checked = true; break;
            }

            /* Load saved parity */
            switch (Settings.Port.Parity)
            {
                case System.IO.Ports.Parity.None: ParityNoneRadioButton.Checked = true; break;
                case System.IO.Ports.Parity.Odd: ParityOddRadioButton.Checked = true; break;
                case System.IO.Ports.Parity.Even: ParityEvenRadioButton.Checked = true; break;
                case System.IO.Ports.Parity.Mark: ParityMarkRadioButton.Checked = true; break;
                case System.IO.Ports.Parity.Space: ParitySpaceRadioButton.Checked = true; break;
            }

            /* Load saved stop bits */
            switch (Settings.Port.StopBits)
            {
                case System.IO.Ports.StopBits.One: StopBits1RadioButton.Checked = true; break;
                case System.IO.Ports.StopBits.OnePointFive: StopBits15RadioButton.Checked = true; break;
                case System.IO.Ports.StopBits.Two: StopBits2RadioButton.Checked = true; break;
            }

            /* Load saved handshake */
            switch (Settings.Port.Handshake)
            {
                case System.IO.Ports.Handshake.None: HandshakingNoneRadioButton.Checked = true; break;
                case System.IO.Ports.Handshake.XOnXOff: HandshakingXONXOFRadioButton.Checked = true; break;
                case System.IO.Ports.Handshake.RequestToSend: HandshakingRTSCTSRadioButton.Checked = true; break;
                case System.IO.Ports.Handshake.RequestToSendXOnXOff: HandshakingRTSCTSXONXOFRadioButton.Checked = true; break;
            }

            /* Load saved DTR and RTS settings */
            TransmitDTRCheckBox.Checked = Settings.Port.Dtr;
            TransmitRTSCheckBox.Checked = Settings.Port.Rts;
        }

        /*----------------------------------------------------------
         * ComPortMonitorTimer_Tick
         * 
         * Updates COM port list dynamically while the dropdown is open.
        *---------------------------------------------------------*/
        private void ComPortMonitorTimer_Tick(object sender, EventArgs e)
        {
            if (!COMPortComboBox.DroppedDown)
                return;

            string[] currentPorts = SerialPort.GetPortNames();

            if (!lastKnownPorts.SequenceEqual(currentPorts))
            {
                COMPortComboBox.DroppedDown = false;
                COMPortComboBox_Click(this, EventArgs.Empty);
                COMPortComboBox.DroppedDown = true;

                lastKnownPorts = currentPorts;
            }
        }

        /*----------------------------------------------------------
         * COMPortComboBox_DropDown
         * 
         * Starts monitoring COM ports when dropdown opens.
        *---------------------------------------------------------*/
        private void COMPortComboBox_DropDown(object sender, EventArgs e)
        {
            lastKnownPorts = SerialPort.GetPortNames();
            comPortMonitorTimer.Start();
        }

        /*----------------------------------------------------------
         * COMPortComboBox_DropDownClosed
         * 
         * Stops monitoring COM ports when dropdown closes.
        *---------------------------------------------------------*/
        private void COMPortComboBox_DropDownClosed(object sender, EventArgs e)
        {
            comPortMonitorTimer.Stop();
        }

        /*----------------------------------------------------------
         * COMPortComboBox_Click
         * 
         * Populates the COM port dropdown with all available ports.
         * - Appends friendly device descriptions from WMI.
         * - Removes duplicates from ports and descriptions.
         * - Selects the last-used COM port if present.
         *---------------------------------------------------------*/
        private void COMPortComboBox_Click(object sender, EventArgs e)
        {
            // Clear existing items
            COMPortComboBox.Items.Clear();

            // Get unique COM ports
            string[] portList = SerialPort.GetPortNames().Distinct().ToArray();

            // Load device descriptions from WMI
            Dictionary<string, string> portDescriptions = new Dictionary<string, string>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Caption FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'");

                foreach (ManagementObject obj in searcher.Get())
                {
                    string caption = obj["Caption"]?.ToString();
                    if (string.IsNullOrWhiteSpace(caption))
                        continue;

                    var match = System.Text.RegularExpressions.Regex.Match(caption, @"\((COM\d+)\)");
                    if (match.Success)
                    {
                        string comPort = match.Groups[1].Value;
                        string desc = caption.Replace($"({comPort})", "").Trim();

                        // Avoid duplicate keys
                        if (!portDescriptions.ContainsKey(comPort))
                            portDescriptions[comPort] = desc;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("COM port description load failed: " + ex.Message);
            }

            // Populate ComboBox without duplicates
            HashSet<string> addedItems = new HashSet<string>();
            int selectedIndex = -1;

            for (int i = 0; i < portList.Length; i++)
            {
                string item = portList[i];
                if (portDescriptions.TryGetValue(portList[i], out string desc))
                    item += " - " + desc;

                if (addedItems.Add(item)) // prevents duplicate entries
                    COMPortComboBox.Items.Add(item);

                // Track selection of last-used port
                if (portList[i] == Settings.Port.PortName)
                    selectedIndex = COMPortComboBox.Items.IndexOf(item);
            }

            // Select last-used port if present
            if (selectedIndex >= 0)
                COMPortComboBox.SelectedIndex = selectedIndex;
        }


        /*----------------------------------------------------------
         * BaudRateRadioButton_Click
         * 
         * Hides custom baud rate input and resizes group box accordingly.
        *---------------------------------------------------------*/
        private void BaudRateRadioButton_Click(object sender, EventArgs e)
        {
            CustomBRTextBox.Visible = CustomBRTextBox.Enabled = CustomBRGroupBox.Visible = CustomBRGroupBox.Enabled = false;
            ComPortGroupBox.Size = new System.Drawing.Size(99, 95);
        }

        /*----------------------------------------------------------
         * BaudRateCustomRadioButton_Click
         * 
         * Shows custom baud rate input and resizes group box accordingly.
        *---------------------------------------------------------*/
        private void BaudRateCustomRadioButton_Click(object sender, EventArgs e)
        {
            CustomBRTextBox.Visible = CustomBRTextBox.Enabled = CustomBRGroupBox.Visible = CustomBRGroupBox.Enabled = true;
            ComPortGroupBox.Size = new System.Drawing.Size(99, 46);
        }

        /*----------------------------------------------------------
         * SaveOptions_Click
         * 
         * Saves all current UI selections to application settings and writes XML.
        *---------------------------------------------------------*/
        private void SaveOptions_Click(object sender, EventArgs e)
        {
            /* Save COM Port */
            Settings.Port.PortName = COMPortComboBox.Text.Split('-')[0].Trim();

            /* Save baud rate */
            if (BaudRateCustomRadioButton.Checked && int.TryParse(CustomBRTextBox.Text, out int customBaud))
                Settings.Port.BaudRate = customBaud;
            else
                Settings.Port.BaudRate = BaudRate600RadioButton.Checked ? 600 :
                                        BaudRate1200RadioButton.Checked ? 1200 :
                                        BaudRate2400RadioButton.Checked ? 2400 :
                                        BaudRate4800RadioButton.Checked ? 4800 :
                                        BaudRate9600RadioButton.Checked ? 9600 :
                                        BaudRate14400RadioButton.Checked ? 14400 :
                                        BaudRate19200RadioButton.Checked ? 19200 :
                                        BaudRate28800RadioButton.Checked ? 28800 :
                                        BaudRate38400RadioButton.Checked ? 38400 :
                                        BaudRate56000RadioButton.Checked ? 56000 :
                                        BaudRate57600RadioButton.Checked ? 57600 :
                                        BaudRate115200RadioButton.Checked ? 115200 :
                                        BaudRate128000RadioButton.Checked ? 128000 :
                                        BaudRate256000RadioButton.Checked ? 256000 : 9600;

            /* Save data bits */
            Settings.Port.DataBits = DataBits5RadioButton.Checked ? 5 :
                                    DataBits6RadioButton.Checked ? 6 :
                                    DataBits7RadioButton.Checked ? 7 : 8;

            /* Save append type */
            Settings.Port.Append = AppendNoneRadioButton.Checked ? Settings.Port.AppendType.AppendNothing :
                                  AppendCRRadioButton.Checked ? Settings.Port.AppendType.AppendCR :
                                  AppendLFRadioButton.Checked ? Settings.Port.AppendType.AppendLF :
                                  Settings.Port.AppendType.AppendCRLF;

            /* Save parity */
            Settings.Port.Parity = ParityNoneRadioButton.Checked ? Parity.None :
                                  ParityOddRadioButton.Checked ? Parity.Odd :
                                  ParityEvenRadioButton.Checked ? Parity.Even :
                                  ParityMarkRadioButton.Checked ? Parity.Mark : Parity.Space;

            /* Save stop bits */
            Settings.Port.StopBits = StopBits1RadioButton.Checked ? StopBits.One :
                                    StopBits15RadioButton.Checked ? StopBits.OnePointFive : StopBits.Two;

            /* Save handshake */
            Settings.Port.Handshake = HandshakingNoneRadioButton.Checked ? Handshake.None :
                                     HandshakingXONXOFRadioButton.Checked ? Handshake.XOnXOff :
                                     HandshakingRTSCTSRadioButton.Checked ? Handshake.RequestToSend :
                                     Handshake.RequestToSendXOnXOff;

            /* Save DTR/RTS */
            Settings.Port.Dtr = TransmitDTRCheckBox.Checked;
            Settings.Port.Rts = TransmitRTSCheckBox.Checked;

            /* Write settings to XML */
            Settings.WriteXml();

            /* Close form */
            Close();
        }

        /*----------------------------------------------------------
         * WndProc
         * 
         * Blocks right-clicks on title bar and borders.
        *---------------------------------------------------------*/
        protected override void WndProc(ref Message buffer)
        {
            if (buffer.Msg != 0xA3)
                base.WndProc(ref buffer);
        }
    }
}
