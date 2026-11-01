/* About_Form.cs -- the About dialog, showing the name, version, copyright and
   license from AssemblyInfo.cs.

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace _4Term
{
    /* The About dialog.  */
    partial class About_Form : Form
    {
        /* Properties that read the assembly information: title, version,
           description, product, copyright and company.  */
        #region Assembly Attribute Accessors
        /* Return the assembly attribute of type T, or null if there is
           none.  */
        private static T GetAssemblyAttribute<T>() where T : Attribute
        {
            return (T)Assembly.GetExecutingAssembly()
                .GetCustomAttributes(typeof(T), false)
                .FirstOrDefault();
        }
        /* The title from AssemblyTitle, or the file name if there is none.  */
        public string AssemblyTitle
        {
            get
            {
                var attr = GetAssemblyAttribute<AssemblyTitleAttribute>();
                if (attr != null && !string.IsNullOrEmpty(attr.Title))
                    return attr.Title;

                return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }
        /* The version as shown to the user: major.minor, with the build
           number only if it is not 0, such as "2.0" or "2.0.1".  */
        public static string ShownVersion
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return version.ToString(version.Build > 0 ? 3 : 2);
            }
        }

        /* The full version, such as 2.0.0.0.  */
        public string AssemblyVersion => Assembly.GetExecutingAssembly().GetName().Version.ToString();
        /* The description from AssemblyDescription.  */
        public string AssemblyDescription => GetAssemblyAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";
        /* The product name.  */
        public string AssemblyProduct => GetAssemblyAttribute<AssemblyProductAttribute>()?.Product ?? "";
        /* The copyright notice.  */
        public string AssemblyCopyright => GetAssemblyAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";
        /* The company name.  */
        public string AssemblyCompany => GetAssemblyAttribute<AssemblyCompanyAttribute>()?.Company ?? "";
        #endregion

        /* Create the dialog and fill in the assembly information.  */
        public About_Form()
        {
            InitializeComponent();

            var assembly = Assembly.GetExecutingAssembly();

            string license = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attr => attr.Key == "License")?.Value ?? "License info not found";

            this.Text = $"About {AssemblyTitle}";
            this.labelProductName.Text = AssemblyProduct;
            this.labelVersion.Text = $"Version {ShownVersion}";
            this.labelCopyright.Text = AssemblyCopyright;
            this.labelCompanyName.Text = AssemblyCompany;
            this.textBoxDescription.Text = AssemblyDescription;
            this.labelMetadata.Text = license;
        }

        /* Close the dialog.  */
        private void CloseButton_Click(object sender, EventArgs e) => Close();

        /* Ignore right-clicks on the title bar and borders (the system menu)
           and double-clicks on the title bar (maximize); pass every other
           message BUFFER on.  */
        protected override void WndProc(ref Message buffer)
        {
            if (!Main_Form.IsBlockedNonClientMessage(buffer.Msg))
                base.WndProc(ref buffer);
        }
    }
}
