/* Program.cs -- entry point of 4Term.

   4Term is a serial port terminal for talking to and logging serial devices,
   with a text and a hex view of the data, macros, scripts and automatic
   reconnection.

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

using System;
using System.Windows.Forms;

namespace _4Term
{
     /* Program startup.  */
     static class Program
    {
        /* Set up error reporting and visual styles, then create the main
           window and run it until it is closed.  */
        [STAThread]
        static void Main()
        {
            /* Report unexpected errors instead of showing the raw crash
               dialog.  */
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) => ShowError(e.Exception, false);
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => ShowError(e.ExceptionObject as Exception, true);

            /* Draw sharply at 125 % and 150 % screen scaling instead of being
               stretched by Windows.  The forms scale with their fonts
               (AutoScaleMode.Font); the macro buffer scales its own sizes.  */
            MakeDpiAware();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Main_Form mainForm = new Main_Form();
            Application.Run(mainForm);
        }

        /* Tell Windows that 4Term handles the screen's dpi itself.  Must run
           before the first window is created.  Does nothing elsewhere, such
           as under Mono on Linux.  */
        private static void MakeDpiAware()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT ||
                Environment.OSVersion.Version.Major < 6)
                return;
            try
            {
                SetProcessDPIAware();
            }
            catch (Exception)
            {
                /* Without it Windows stretches the window; that is all.  */
            }
        }

        /* If EX is a font failing in Font.ToLogFont, return a list of the
           controls whose font is no longer valid, to find the cause;
           otherwise return "".  */
        private static string BrokenFontReport(Exception ex)
        {
            if (!(ex is ArgumentException) || ex.StackTrace == null || !ex.StackTrace.Contains("ToLogFont"))
                return "";

            /* The native font handle: "nativeFont" in the .NET Framework,
               "fontObject" in Mono.  It is zero once the font is
               disposed.  */
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var field = typeof(System.Drawing.Font).GetField("nativeFont", flags) ??
                        typeof(System.Drawing.Font).GetField("fontObject", flags);
            if (field == null)
                return "";

            var broken = new System.Collections.Generic.List<string>();
            foreach (Form form in Application.OpenForms)
                FindBrokenFonts(form, "", field, broken);

            if (broken.Count == 0)
                return "\n\nNo control has a disposed font.";
            return "\n\nControls with a disposed font:\n" + string.Join("\n", broken.GetRange(0, Math.Min(8, broken.Count))) +
                   (broken.Count > 8 ? $"\n... and {broken.Count - 8} more" : "");
        }

        /* Add to BROKEN the path of CONTROL, under PATH, and of each control
           in it whose font's native handle, FIELD, is zero.  */
        private static void FindBrokenFonts(Control control, string path, System.Reflection.FieldInfo field,
                                            System.Collections.Generic.List<string> broken)
        {
            string name = string.IsNullOrEmpty(control.Name) ? control.GetType().Name : control.Name;
            string here = path.Length == 0 ? name : path + " > " + name;
            if ((IntPtr)field.GetValue(control.Font) == IntPtr.Zero)
                broken.Add(here);
            foreach (Control child in control.Controls)
                FindBrokenFonts(child, here, field, broken);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        /* Show the unexpected error EX in a message box.  FATAL is true for
           errors on other threads, after which the process ends; errors on the
           UI thread are recoverable.  */
        private static void ShowError(Exception ex, bool fatal)
        {
            string message = (ex?.Message ?? "Unknown error") + BrokenFontReport(ex);
            MessageBox.Show(
                fatal ? $"A fatal error occurred and 4Term must close:\n\n{message}"
                      : $"An unexpected error occurred:\n\n{message}",
                "4Term Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}