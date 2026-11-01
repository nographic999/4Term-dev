/* Xml.cs -- reading and writing config.xml, the settings file kept next to
   4Term.exe.  Values are addressed by paths such as "Port/BaudRate".

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

using System.Xml;
using System.Windows.Forms;

namespace _4Term
{
    /* Access to the configuration file.  */
    public class Xml
    {
        /* Full path of the configuration file, next to the executable rather
           than in the working directory.  */
        public static string FileName = System.IO.Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory, "config.xml");

        /* Error message from the last LoadFile if the file exists but could
           not be parsed, otherwise null.  */
        public static string LoadError = null;

        /* The loaded document, or null when no file is loaded.  */
        public static System.Xml.XmlDocument XmlFile = null;

        /* Root element of XmlFile, or null when no file is loaded.  */
        public static System.Xml.XmlElement RootElement = null;

        /* Create an empty configuration file if none exists, asking the user
           first when ASK is true.  Return true if the file exists afterwards,
           or false if the user declined or the file could not be written.  */
        public static bool CreateNew(bool ask = true)
        {
            /* Nothing to do if the file exists.  */
            if (System.IO.File.Exists(FileName))
                return true;

            /* Ask the user, unless ASK is false.  */
            DialogResult result = !ask ? DialogResult.Yes : MessageBox.Show(
                "The configuration file does not exist. Would you like to create a new configuration file?",
                "Create New Configuration",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    XmlFile = new System.Xml.XmlDocument();

                    /* Write the XML declaration explicitly.  */
                    System.Xml.XmlDeclaration xmlDeclaration = XmlFile.CreateXmlDeclaration("1.0", "UTF-8", null);
                    XmlFile.AppendChild(xmlDeclaration);

                    /* Create the <Settings> root element.  */
                    System.Xml.XmlElement root = XmlFile.CreateElement("Settings");
                    XmlFile.AppendChild(root);

                    /* Save the XML document.  */
                    XmlFile.Save(FileName);

                    RootElement = root;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        /* Load the configuration file into XmlFile.  Return true on success.
           Return false if the file does not exist, or if it cannot be parsed
           or has no root element, in which case LoadError says why.  */
        public static bool LoadFile()
        {
            LoadError = null;

            /* A missing file is not an error.  */
            if (!System.IO.File.Exists(FileName))
                return false;

            /* Parse the file into XmlFile.  */
            try
            {
                XmlFile = new System.Xml.XmlDocument();
                XmlFile.Load(FileName);
            }
            catch (System.Exception ex)
            {
                XmlFile = null;
                RootElement = null;
                LoadError = ex.Message;
                return false;
            }

            /* Verify the root element exists.  */
            RootElement = XmlFile.DocumentElement;
            if (RootElement == null)
            {
                LoadError = "The file has no root element.";
                return false;
            }

            /* Loaded, with a root element.  */
            return true;
        }

        /* Return the boolean at XPATH, or FALLBACK if it is missing or not a
           valid boolean.  */
        public static bool ReadBoolParm(string xpath, bool fallback = false)
        {
            string text = ReadStringParm(xpath);
            return text != null && bool.TryParse(text, out bool result) ? result : fallback;
        }

        /* Return the integer at XPATH, or FALLBACK if it is missing or not a
           valid integer.  */
        public static int ReadIntParm(string xpath, int fallback = -1)
        {
            string text = ReadStringParm(xpath);
            return text != null && int.TryParse(text, out int result) ? result : fallback;
        }

        /* Return the number at XPATH, written with a dot as decimal separator,
           or FALLBACK if it is missing or invalid.  */
        public static float ReadFloatParm(string xpath, float fallback)
        {
            string text = ReadStringParm(xpath);
            return text != null && float.TryParse(text,
                       System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out float result)
                ? result : fallback;
        }

        /* Return the text at XPATH, or FALLBACK if the node is missing.  */
        public static string ReadStringParm(string xpath, string fallback = null)
        {
            var node = RootElement.SelectSingleNode(xpath);
            return node != null ? node.InnerText : fallback;
        }

        /* Store VALUE at XPATH as "true" or "false", creating missing nodes on
           the way.  Always return true.  */
        public static bool WriteBoolParm(string xpath, bool value) =>
            WriteStringParm(xpath, value.ToString().ToLower());

        /* Store the integer VALUE at XPATH, creating missing nodes on the way.
           Always return true.  */
        public static bool WriteIntParm(string xpath, int value) =>
            WriteStringParm(xpath, value.ToString());

        /* Store the number VALUE at XPATH with a dot as decimal separator.
           Always return true.  */
        public static bool WriteFloatParm(string xpath, float value) =>
            WriteStringParm(xpath, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /* Store the text VALUE at XPATH, creating missing nodes on the way.
           Always return true.  */
        public static bool WriteStringParm(string xpath, string value)
        {
            /* Walk down the path from the root, creating each missing
               node.  */
            System.Xml.XmlNode current = RootElement;
            foreach (string part in xpath.Split('/'))
            {
                var next = current.SelectSingleNode(part);
                if (next == null)
                {
                    next = XmlFile.CreateElement(part);
                    current.AppendChild(next);
                }
                current = next;
            }

            current.InnerText = value;
            return true;
        }

        /* Save the loaded document to the configuration file, indented, and
            release it.  */
	    public static void CloseFile()
	    {
            /* Write the file indented, keeping the XML declaration.  */
	        using (var writer = System.Xml.XmlWriter.Create(FileName, new System.Xml.XmlWriterSettings
	        {
		    Indent = true,
		    OmitXmlDeclaration = false,
		    NewLineHandling = NewLineHandling.None
	        }))
		      {
		          XmlFile.Save(writer);
		      }

            /* Release the document.  */
	        XmlFile = null;
	        RootElement = null;
	    }
    }
}
