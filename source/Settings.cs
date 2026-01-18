using System;
using System.IO.Ports;

namespace _4Term
{
    public class Settings
    {
	    /*----------------------------------------------------------
	     * Class variables
	     * -------------------------------------------------------*/

        public class Program
        {
            public static bool FirstStart = true;
            public static bool ShowWelcomeMessage = false;
            public static bool IsSwapButtonVisible = true;
        }

        public class Form
        {
            public static int Width = 718;
            public static int Height = 525;
            public static bool AdvancedMode = false;
            public static bool ExternalTools = false;
        }

        public class RichTextBox
        {
            public static string Font = "Consolas";
            public static string FontStyle = "Regular";
            public static int Size = 10;
            public static bool Strikeout = false;
            public static bool Underline = false;
            public static bool WordWrap = true;
            public static bool Scroll = true;
            public static bool HexOutput = false;
            public static bool LocalEcho = true;            
        }

        public class Color
        {
            public static (int R, int G, int B) Back = (30, 30, 30);
            public static (int R, int G, int B) Transmit = (194, 30, 86);
            public static (int R, int G, int B) Receive = (152, 255, 204);
        }      
        
        public class Port
        {      
            public static string PortName = "COM1";
            public static int BaudRate = 9600;
            public static int DataBits = 8;
            public static System.IO.Ports.Parity Parity = System.IO.Ports.Parity.None;
            public static System.IO.Ports.StopBits StopBits = System.IO.Ports.StopBits.One;
            public static System.IO.Ports.Handshake Handshake = System.IO.Ports.Handshake.None;
            public static bool Dtr = false;
            public static bool Rts = false;

            public enum AppendType
            {
                AppendNothing,
                AppendCR,
                AppendLF,
                AppendCRLF
            }

            public static AppendType Append = AppendType.AppendCR;
        }

        public class Page
        {
            public static string[] Text = new string[16]; // set 16 to 32 for 4 pages
        }

        public class MacroText
        {
            public static string[] Text = new string[256]; // set 256 to 512 for 4 pages
        }

        public class MacroCommand
        {
            public static string[] Text = new string[256]; // set 256 to 512 for 4 pages
        }

        /*----------------------------------------------------------
         * Class Functions
         * -------------------------------------------------------*/

        /*----------------------------------------------------------
         * ReadXml
         * 
         * Loads application settings from the XML configuration file.
         * Initializes UI dimensions, font styles, serial port settings,
         * color themes, and macro definitions.
         * Exits if file creation fails.
         *---------------------------------------------------------*/
        public static void ReadXml()
        {
            /* Load XML file */
            if (!Xml.LoadFile())
            {
                if (!Xml.CreateNew())
                {
                    Environment.Exit(1);
                }
                WriteXml();
                Xml.LoadFile();
            }

            /* Program section */
            Program.FirstStart = Xml.ReadBoolParm("Program/FirstStart");
            Program.ShowWelcomeMessage = Xml.ReadBoolParm("Program/ShowWelcomeMessage");
            Program.IsSwapButtonVisible = Xml.ReadBoolParm("Program/IsSwapButtonVisible");

            /* Form section */
            Form.Width = Xml.ReadIntParm("Form/Width");
            Form.Height = Xml.ReadIntParm("Form/Height");
            Form.AdvancedMode = Xml.ReadBoolParm("Form/AdvancedMode");
            Form.ExternalTools = Xml.ReadBoolParm("Form/ExternalTools");

            /* RichTextBox section */
            RichTextBox.Font = Xml.ReadStringParm("RichTextBox/Font");
            RichTextBox.FontStyle = Xml.ReadStringParm("RichTextBox/FontStyle");
            RichTextBox.Size = Xml.ReadIntParm("RichTextBox/Size");
            RichTextBox.Strikeout = Xml.ReadBoolParm("RichTextBox/Strikeout");
            RichTextBox.Underline = Xml.ReadBoolParm("RichTextBox/Underline");
            RichTextBox.WordWrap = Xml.ReadBoolParm("RichTextBox/WordWrap");
            RichTextBox.Scroll = Xml.ReadBoolParm("RichTextBox/Scroll");
            RichTextBox.HexOutput = Xml.ReadBoolParm("RichTextBox/HexOutput");
            RichTextBox.LocalEcho = Xml.ReadBoolParm("RichTextBox/LocalEcho");            

            /* Color section */
            Color.Back.R = Xml.ReadIntParm("Back/R");
            Color.Back.G = Xml.ReadIntParm("Back/G");
            Color.Back.B = Xml.ReadIntParm("Back/B");
            Color.Transmit.R = Xml.ReadIntParm("Transmit/R");
            Color.Transmit.G = Xml.ReadIntParm("Transmit/G");
            Color.Transmit.B = Xml.ReadIntParm("Transmit/B");
            Color.Receive.R = Xml.ReadIntParm("Receive/R");
            Color.Receive.G = Xml.ReadIntParm("Receive/G");
            Color.Receive.B = Xml.ReadIntParm("Receive/B");

            /* Port section */
            Port.PortName = Xml.ReadStringParm("Port/PortName");
            Port.BaudRate = Xml.ReadIntParm("Port/BaudRate");
            Port.DataBits = Xml.ReadIntParm("Port/DataBits");
            Port.Parity = (Parity)Enum.Parse(typeof(Parity), Xml.ReadStringParm("Port/Parity"));
            Port.StopBits = (StopBits)Enum.Parse(typeof(StopBits), Xml.ReadStringParm("Port/StopBits"));
            Port.Handshake = (Handshake)Enum.Parse(typeof(Handshake), Xml.ReadStringParm("Port/Handshake"));
            Port.Append = (Port.AppendType)Enum.Parse(typeof(Port.AppendType), Xml.ReadStringParm("Port/AppendToSend"));
            Port.Dtr = Xml.ReadBoolParm("Port/Dtr");
            Port.Rts = Xml.ReadBoolParm("Port/Rts");

            /* Page section */
            for (int i = 0; i < Page.Text.Length; i++)
            {
                Page.Text[i] = Xml.ReadStringParm($"Page/Item{i}");
            }

            /* MacroText section */
            for (int i = 0; i < MacroText.Text.Length; i++)
            {
                MacroText.Text[i] = Xml.ReadStringParm($"MacroText/Item{i}");
            }

            /* MacroCommand section */
            for (int i = 0; i < MacroCommand.Text.Length; i++)
            {
                MacroCommand.Text[i] = Xml.ReadStringParm($"MacroCommand/Item{i}");
            }
        }

        /*----------------------------------------------------------
         * WriteXml
         * 
         * Saves current application settings to the XML configuration file.
         * Persists UI dimensions, font styles, color settings, port configurations,
         * and all macro-related text and commands.
         * Exits early if the file cannot be loaded.
         *---------------------------------------------------------*/
        public static void WriteXml()
        {
            /* Load XML file */
            if (!Xml.LoadFile())
                return;

            /* Program section */
            Xml.WriteBoolParm("Program/FirstStart", Program.FirstStart);
            Xml.WriteBoolParm("Program/ShowWelcomeMessage", Program.ShowWelcomeMessage);
            Xml.WriteBoolParm("Program/IsSwapButtonVisible", Program.IsSwapButtonVisible);

            /* Form section */
            Xml.WriteIntParm("Form/Width", Form.Width);
            Xml.WriteIntParm("Form/Height", Form.Height);
            Xml.WriteBoolParm("Form/AdvancedMode", Form.AdvancedMode);
            Xml.WriteBoolParm("Form/ExternalTools", Form.ExternalTools);

            /* RichTextBox section */
            Xml.WriteStringParm("RichTextBox/Font", RichTextBox.Font);
            Xml.WriteStringParm("RichTextBox/FontStyle", RichTextBox.FontStyle);
            Xml.WriteIntParm("RichTextBox/Size", RichTextBox.Size);
            Xml.WriteBoolParm("RichTextBox/Strikeout", RichTextBox.Strikeout);
            Xml.WriteBoolParm("RichTextBox/Underline", RichTextBox.Underline);
            Xml.WriteBoolParm("RichTextBox/WordWrap", RichTextBox.WordWrap);
            Xml.WriteBoolParm("RichTextBox/Scroll", RichTextBox.Scroll);
            Xml.WriteBoolParm("RichTextBox/HexOutput", RichTextBox.HexOutput);
            Xml.WriteBoolParm("RichTextBox/LocalEcho", RichTextBox.LocalEcho);
            
            /* Color section */
            Xml.WriteIntParm("Back/R", Color.Back.R);
            Xml.WriteIntParm("Back/G", Color.Back.G);
            Xml.WriteIntParm("Back/B", Color.Back.B);
            Xml.WriteIntParm("Transmit/R", Color.Transmit.R);
            Xml.WriteIntParm("Transmit/G", Color.Transmit.G);
            Xml.WriteIntParm("Transmit/B", Color.Transmit.B);
            Xml.WriteIntParm("Receive/R", Color.Receive.R);
            Xml.WriteIntParm("Receive/G", Color.Receive.G);
            Xml.WriteIntParm("Receive/B", Color.Receive.B);

            /* Port section */
            Xml.WriteStringParm("Port/PortName", Port.PortName);
            Xml.WriteIntParm("Port/BaudRate", Port.BaudRate);
            Xml.WriteIntParm("Port/DataBits", Port.DataBits);
            Xml.WriteStringParm("Port/Parity", Port.Parity.ToString());
            Xml.WriteStringParm("Port/StopBits", Port.StopBits.ToString());
            Xml.WriteStringParm("Port/Handshake", Port.Handshake.ToString());
            Xml.WriteStringParm("Port/AppendToSend", Port.Append.ToString());
            Xml.WriteBoolParm("Port/Dtr", Port.Dtr);
            Xml.WriteBoolParm("Port/Rts", Port.Rts);

            /* Page section */
            for (int i = 0; i < Page.Text.Length; i++)
            {
                Xml.WriteStringParm($"Page/Item{i}", Page.Text[i]);
            }

            /* MacroText section */
            for (int i = 0; i < MacroText.Text.Length; i++)
            {
                Xml.WriteStringParm($"MacroText/Item{i}", MacroText.Text[i]);
            }

            /* MacroCommand section */
            for (int i = 0; i < MacroCommand.Text.Length; i++)
            {
                Xml.WriteStringParm($"MacroCommand/Item{i}", MacroCommand.Text[i]);
            }

            /* Save and close */
            Xml.CloseFile();
        }
    }
}
