using System;
using System.Xml;
using System.Windows.Forms;

namespace _4Term
{
    public class Xml
    {
        /*----------------------------------------------------------
         * Class variables
         * -------------------------------------------------------*/

        /* XML configuration filename */
        public static string FileName = "config.xml";

        /* Loaded XML document instance */
        public static System.Xml.XmlDocument XmlFile = null;

        /* Root element of the loaded XML document */
        public static System.Xml.XmlElement RootElement = null;

        /*----------------------------------------------------------
         * Class Functions
         * -------------------------------------------------------*/

        /*----------------------------------------------------------
         * CreateNew
         *
         * Prompts the user to create a new XML configuration file
         * if it doesn't exist. Returns true if the file was created.
         *---------------------------------------------------------*/
        public static bool CreateNew()
        {
            /* Check if file exists */
            if (System.IO.File.Exists(FileName))
                return true;

            /* Show message box */
            DialogResult result = MessageBox.Show(
                "The configuration file does not exist. Would you like to create a new configuration file?",
                "Create New Configuration",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    XmlFile = new System.Xml.XmlDocument();

                    /* Create XML declaration explicitly */
                    System.Xml.XmlDeclaration xmlDeclaration = XmlFile.CreateXmlDeclaration("1.0", "UTF-8", null);
                    XmlFile.AppendChild(xmlDeclaration);

                    /* Create <Settings> root element */
                    System.Xml.XmlElement root = XmlFile.CreateElement("Settings");
                    XmlFile.AppendChild(root);

                    /* Save the XML document */
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

        /*----------------------------------------------------------
         * LoadFile
         * 
         * Loads XML file into memory.
         * Returns true if successful.
         *---------------------------------------------------------*/
        public static bool LoadFile()
        {
            /* Check if file exists */
            if (!System.IO.File.Exists(FileName))
                return false;

            /* Load XML file into the class-level XmlFile */
            XmlFile = new System.Xml.XmlDocument();
            XmlFile.Load(FileName);

            /* Optionally, you can verify the root element exists */
            RootElement = XmlFile.DocumentElement;
            if (RootElement == null)
                return false;

            /* Return true if load succeeded and root element is found */
            return true;
        }

        /*----------------------------------------------------------
         * ReadBoolParm
         * 
         * Reads a bool from loaded XML at XPath.
         * Returns false if not found or invalid.
         *---------------------------------------------------------*/
        public static bool ReadBoolParm(string xpath)
        {
            /* Find the boolean node */
            var BoolNode = RootElement.SelectSingleNode(xpath);
            if (BoolNode != null && bool.TryParse(BoolNode.InnerText, out bool result))
            {
                return result;
            }

            /* Return false if node not found or value is not a valid bool */
            return false;
        }

        /*----------------------------------------------------------
         * ReadIntParm
         * 
         * Reads an int from loaded XML at XPath.
         * Returns -1 if not found or invalid.
         *---------------------------------------------------------*/
        public static int ReadIntParm(string xpath)
        {
            /* Find the integer node */
            var IntNode = RootElement.SelectSingleNode(xpath);
            if (IntNode != null && int.TryParse(IntNode.InnerText, out int result))
            {
                return result;
            }

            /* Return -1 if node not found or value is not a valid int */
            return -1;
        }

        /*----------------------------------------------------------
         * ReadStringParm
         * 
         * Reads a string from loaded XML at XPath.
         * Returns null if not found.
         *---------------------------------------------------------*/
        public static string ReadStringParm(string xpath)
        {
            /* Find the string node */
            var StringNode = RootElement.SelectSingleNode(xpath);
            if (StringNode != null)
            {
                return StringNode.InnerText;
            }

            /* Return null if node not found */
            return null;
        }

        /*----------------------------------------------------------
         * WriteBoolParm
         * 
         * Writes a bool to loaded XML node at XPath.
         * Returns true if successful.
         *---------------------------------------------------------*/
        public static bool WriteBoolParm(string xpath, bool value)
        {
            /* Split the XPath into individual node names */
            string[] parts = xpath.Split('/');

            /* Start from the root element */
            System.Xml.XmlNode current = RootElement;

            /* Traverse or create each node along the XPath */
            foreach (string part in parts)
            {
                var next = current.SelectSingleNode(part);

                /* If node doesn't exist, create it */
                if (next == null)
                {
                    next = XmlFile.CreateElement(part);
                    current.AppendChild(next);
                }

                /* Move to the next node */
                current = next;
            }

            /* Set the node's inner text to the bool value */
            current.InnerText = value.ToString().ToLower();

            /* Indicate success */
            return true;
        }


        /*----------------------------------------------------------
         * WriteIntParm
         * 
         * Writes an int to loaded XML node at XPath.
         * Returns true if successful.
         *---------------------------------------------------------*/
        public static bool WriteIntParm(string xpath, int value)
        {
            /* Split the XPath into individual node names */
            string[] parts = xpath.Split('/');

            /* Start from the root element */
            System.Xml.XmlNode current = RootElement;

            /* Traverse or create each node along the XPath */
            foreach (string part in parts)
            {
                var next = current.SelectSingleNode(part);

                /* If node doesn't exist, create it */
                if (next == null)
                {
                    next = XmlFile.CreateElement(part);
                    current.AppendChild(next);
                }

                /* Move to the next node */
                current = next;
            }

            /* Set the node's inner text to the intvalue */
            current.InnerText = value.ToString();

            /* Indicate success */
            return true;
        }


        /*----------------------------------------------------------
         * WriteStringParm
         * 
         * Writes a string to loaded XML node at XPath.
         * Returns true if successful.
         *---------------------------------------------------------*/
        public static bool WriteStringParm(string xpath, string value)
        {
            /* Split the XPath into individual node names */
            string[] parts = xpath.Split('/');

            /* Start from the root element */
            System.Xml.XmlNode current = RootElement;

            /* Traverse or create each node along the XPath */
            foreach (string part in parts)
            {
                var next = current.SelectSingleNode(part);

                /* If node doesn't exist, create it */
                if (next == null)
                {
                    next = XmlFile.CreateElement(part);
                    current.AppendChild(next);
                }

                /* Move to the next node */
                current = next;
            }

            /* Set the node's inner text to the string value */
            current.InnerText = value;

            /* Indicate success */
            return true;
        }

        /*----------------------------------------------------------
	 * CloseFile
	 *
	 * Saves the current XML document to disk and then clears it
	 * from memory to release resources.
	 *---------------------------------------------------------*/
	public static void CloseFile()
	{
	    // Save the XML file using XmlWriter with custom settings
	    using (var writer = System.Xml.XmlWriter.Create(FileName, new System.Xml.XmlWriterSettings
	    {
		Indent = true,
		OmitXmlDeclaration = false,
		NewLineHandling = NewLineHandling.None
	    }))
		  {
		      XmlFile.Save(writer);
		  }

	    // Clear references from memory
	    XmlFile = null;
	    RootElement = null;
	}
    }
}
