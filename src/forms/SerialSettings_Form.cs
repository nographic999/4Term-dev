using System;
using System.Windows.Forms;
using System.IO.Ports;
using System.Drawing;

namespace _4Term
{
    public partial class SerialPortOptionsForm : Form
    {
        public SerialPortOptionsForm()
        {
            InitializeComponent();
            COMPortComboBox_Click(this, EventArgs.Empty);
            Int32[] baudRates = {
                100,300,600,1200,2400,4800,9600,14400,19200,
                38400,56000,57600,115200,128000,256000,460800,921600,0
            };

            int found = 0;
            for (int i = 0; baudRates[i] != 0; ++i)
            {
                BaudRateComboBox.Items.Add(baudRates[i].ToString());
                if (baudRates[i] == Settings.Port.BaudRate)
                    found = i;
            }
            BaudRateComboBox.SelectedIndex = found;
            DataBitsComboBox.Items.Add("5");
            DataBitsComboBox.Items.Add("6");
            DataBitsComboBox.Items.Add("7");
            DataBitsComboBox.Items.Add("8");
            DataBitsComboBox.SelectedIndex = Settings.Port.DataBits - 5;
            foreach (string s in Enum.GetNames(typeof(Parity)))
            {
                ParityComboBox.Items.Add(s);
            }
            ParityComboBox.SelectedIndex = (int)Settings.Port.Parity;
            foreach (string s in Enum.GetNames(typeof(StopBits)))
            {
                StopBitsComboBox.Items.Add(s);
            }
            StopBitsComboBox.SelectedIndex = (int)Settings.Port.StopBits;
            foreach (string s in Enum.GetNames(typeof(Handshake)))
            {
                HandshakingComboBox.Items.Add(s);
            }
            HandshakingComboBox.SelectedIndex = (int)Settings.Port.Handshake;
            DtrCheckBox.Checked = Settings.Port.Dtr;
            RtsCheckBox.Checked = Settings.Port.Rts;
        }

        private void SaveSettings_Click(object sender, EventArgs e)
        {
            Settings.Port.PortName = COMPortComboBox.Text;
            Settings.Port.BaudRate = Int32.Parse(BaudRateComboBox.Text);
            Settings.Port.DataBits = DataBitsComboBox.SelectedIndex + 5;
            Settings.Port.Parity = (Parity)ParityComboBox.SelectedIndex;
            Settings.Port.StopBits = (StopBits)StopBitsComboBox.SelectedIndex;
            Settings.Port.Handshake = (Handshake)HandshakingComboBox.SelectedIndex;
            Settings.Port.Dtr = DtrCheckBox.Checked;
            Settings.Port.Rts = RtsCheckBox.Checked;
            Settings.WriteXml();
            Close();
        }

        private void COMPortComboBox_Click(object sender, EventArgs e)
        {
            COMPortComboBox.Items.Clear();
            Serial Serial = Serial.Instance;
            int found = 0;
            string[] portList = Serial.GetAvailablePorts();
            for (int i = 0; i < portList.Length; ++i)
            {
                string name = portList[i];
                COMPortComboBox.Items.Add(name);
                if (name == Settings.Port.PortName)
                    found = i;
            }
            if (portList.Length > 0)
                COMPortComboBox.SelectedIndex = found;
        }
    }
}
