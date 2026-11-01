/* HexView.cs -- the hex view of the output window and the log size limit.
   Part of the Main_Form class.

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace _4Term
{
    public partial class Main_Form : Form
    {
        /* Hex view.  Data is shown as a classic hex dump, one line per block
           of bytes, sized to fit the window width:

             RX 00000010  48 65 6C 6C 6F 20 57 6F  72 6C 64 0D 0A 4F 4B 0D  │Hello World··OK·│

           Direction (TX or RX) and offset on the left, bytes in groups of 8,
           printable ASCII on the right with '·' for the rest.  Each direction
           has its own running offset, and a change of direction starts a new
           line.  A partly filled line is redrawn in place as more bytes
           arrive.  */

        /* Bytes of the line being filled.  */
        private readonly List<byte> HexLine = new List<byte>(64);
        /* Direction of the current line: 0 for TX, 1 for RX.  */
        private int HexLineDirection = 0;
        /* Index in the text box where the current line starts, or -1 if there
           is none.  */
        private int HexLineStart = -1;
        /* Text box length right after the current line was drawn.  Any other
           length means something else changed the text, and the line is
           closed.  */
        private int HexExpectedLength = -1;
        /* Bytes per line of the current line.  */
        private int HexBytesPerLine = 16;
        /* Running offsets for TX and RX.  */
        private readonly long[] HexOffset = new long[2];

        /* Font that HexCharWidth was measured for.  */
        private Font HexMeasuredFont = null;
        /* Width of one character of HexMeasuredFont, in pixels.  */
        private float HexCharWidth = 8f;

        /* Column separator, and the glyph shown for non-printable bytes.  */
        private const string HexBar = "│";
        private const char HexDot = '·';

        /* The View > Hex View menu item, created in code.  */
        private ToolStripMenuItem HexViewToolStripMenuItem;

        /* Add "Hex View" (Ctrl+Shift+H) to the View menu, after Toggle
           Scrolling.  */
        private void CreateHexViewMenuItem()
        {
            HexViewToolStripMenuItem = new ToolStripMenuItem("&Hex View")
            {
                Name = "HexViewToolStripMenuItem",
                ShortcutKeys = Keys.Control | Keys.Shift | Keys.H,
                Checked = Settings.Output.HexOutput
            };
            HexViewToolStripMenuItem.Click += HexViewToolStripMenuItem_Click;

            int index = ViewToolStripMenuItem.DropDownItems.IndexOf(ToggleScrollingToolStripMenuItem);
            ViewToolStripMenuItem.DropDownItems.Insert(index + 1, HexViewToolStripMenuItem);
        }

        /* Switch hex view on or off and save the setting.  */
        private void HexViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EndHexLine();
            Settings.Output.HexOutput = !Settings.Output.HexOutput;
            HexViewToolStripMenuItem.Checked = Settings.Output.HexOutput;
            Settings.WriteXml();
        }

        /* Add BYTES to the hex dump.  DIRECTION is 0 for sent and 1 for
           received data.  */
        private void AppendHex(byte[] bytes, int direction)
        {
            /* Close the current line if the direction changed or the text was
               changed elsewhere.  */
            if (HexLine.Count > 0 && (direction != HexLineDirection || !HexLineIsCurrent()))
                EndHexLine();

            int index = 0;
            while (index < bytes.Length)
            {
                /* Start a new line.  */
                if (HexLine.Count == 0)
                {
                    /* Empty text box (cleared): restart the offsets.  */
                    if (RichTextBox.TextLength == 0)
                        HexOffset[0] = HexOffset[1] = 0;

                    /* Start on a fresh line.  */
                    if (!AtLineStart())
                        AppendRaw("\n", ReceiveColor);

                    HexLineDirection = direction;
                    HexBytesPerLine = ComputeHexBytesPerLine();
                    HexLineStart = RichTextBox.TextLength;
                }

                /* Fill the current line.  */
                int take = Math.Min(HexBytesPerLine - HexLine.Count, bytes.Length - index);
                for (int i = 0; i < take; i++)
                    HexLine.Add(bytes[index + i]);
                index += take;

                /* Line full: draw it complete and move on.  */
                if (HexLine.Count == HexBytesPerLine)
                {
                    DrawHexLine(true);
                    HexOffset[HexLineDirection] += HexLine.Count;
                    HexLine.Clear();
                    HexLineStart = -1;
                }
            }

            /* Draw the partly filled line.  */
            if (HexLine.Count > 0)
                DrawHexLine(false);
        }

        /* Close a partly filled hex line so that other text can follow.  */
        private void EndHexLine()
        {
            if (HexLine.Count == 0)
                return;

            if (HexLineIsCurrent())
                AppendRaw("\n", ReceiveColor);

            HexOffset[HexLineDirection] += HexLine.Count;
            HexLine.Clear();
            HexLineStart = -1;
            HexExpectedLength = -1;
        }

        /* Return true if the drawn partial line is still the last thing in the
           text box, that is, it was not cleared, edited, trimmed away or
           followed by other text.  */
        private bool HexLineIsCurrent() =>
            HexLineStart >= 0 && HexExpectedLength == RichTextBox.TextLength;

        /* Draw the current line at the end of the text box, replacing its
           previous partial drawing.  COMPLETE is true when the line is full;
           it then ends with a newline.  */
        private void DrawHexLine(bool complete)
        {
            /* Remove the previous drawing of this line.  */
            if (HexLineStart >= 0 && HexLineStart < RichTextBox.TextLength && HexExpectedLength == RichTextBox.TextLength)
            {
                RichTextBox.Select(HexLineStart, RichTextBox.TextLength - HexLineStart);
                RichTextBox.SelectedText = "";
            }
            HexLineStart = RichTextBox.TextLength;

            int count = HexLine.Count;
            int perLine = HexBytesPerLine;

            StringBuilder hex = new StringBuilder(perLine * 3 + perLine / 8);
            StringBuilder ascii = new StringBuilder(perLine);

            for (int i = 0; i < perLine; i++)
            {
                if (i < count)
                {
                    byte b = HexLine[i];
                    hex.Append(b.ToString("X2")).Append(' ');
                    ascii.Append(b >= 0x20 && b < 0x7F ? (char)b : HexDot);
                }
                else
                {
                    hex.Append("   ");
                    ascii.Append(' ');
                }

                /* Extra space between groups of 8.  */
                if (i % 8 == 7 && i < perLine - 1)
                    hex.Append(' ');
            }

            Color dataColor = HexLineDirection == 0 ? TransmitColor : ReceiveColor;
            Color asciiColor = Blend(dataColor, BackgroundColor, 0.85);
            Color frameColor = Blend(ForeColorFor(BackgroundColor), BackgroundColor, 0.45);

            string prefix = (HexLineDirection == 0 ? "TX " : "RX ") + HexOffset[HexLineDirection].ToString("X8") + "  ";

            AppendRaw(prefix, frameColor);
            AppendRaw(hex.ToString(), dataColor);
            AppendRaw(HexBar, frameColor);
            AppendRaw(ascii.ToString(), asciiColor);
            AppendRaw(complete ? HexBar + "\n" : HexBar, frameColor);

            HexExpectedLength = RichTextBox.TextLength;
        }

        /* Return the largest of 64, 32, 16 or 8 bytes per line whose line fits
           the width of the text box.  A line of N bytes takes 14 + 4N + N/8
           characters: 13 for the prefix, 3N + N/8 - 1 for the hex part and N +
           2 for the ASCII part with its separators.  */
        private int ComputeHexBytesPerLine()
        {
            if (!ReferenceEquals(HexMeasuredFont, RichTextBox.Font))
            {
                HexMeasuredFont = RichTextBox.Font;
                Size size = TextRenderer.MeasureText(new string('0', 64), HexMeasuredFont,
                    Size.Empty, TextFormatFlags.NoPadding);
                HexCharWidth = Math.Max(1f, size.Width / 64f);
            }

            int available = (int)((RichTextBox.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8) / HexCharWidth);

            for (int n = 64; n > 8; n /= 2)
            {
                if (14 + 4 * n + n / 8 <= available)
                    return n;
            }
            return 8;
        }

        /* Return true if the next appended text begins a new line.  */
        private bool AtLineStart()
        {
            int length = RichTextBox.TextLength;
            if (length == 0)
                return true;

            int line = RichTextBox.GetLineFromCharIndex(length);
            return RichTextBox.GetFirstCharIndexFromLine(line) == length;
        }

        /* Log size limit.  */

        /* Keep the text box under Settings.Output.MaxLogMB million
           characters.  When the limit is exceeded, remove the oldest text down
           to three quarters of the limit, cut at a line boundary, so trimming
           happens rarely rather than on every append.  Return the number of
           characters removed.  */
        private int TrimLog()
        {
            long limit = (long)Settings.Output.MaxLogMB * 1024 * 1024;
            int length = RichTextBox.TextLength;
            if (length <= limit)
                return 0;

            int remove = (int)(length - limit * 3 / 4);

            /* Cut at the start of the next line.  */
            int line = RichTextBox.GetLineFromCharIndex(remove);
            int nextLineStart = RichTextBox.GetFirstCharIndexFromLine(line + 1);
            if (nextLineStart > 0 && nextLineStart < length)
                remove = nextLineStart;

            RichTextBox.Select(0, remove);
            RichTextBox.SelectedText = "";

            /* Keep the hex line position in step.  */
            if (HexLineStart >= 0)
            {
                HexLineStart -= remove;
                HexExpectedLength -= remove;
                if (HexLineStart < 0)
                {
                    HexLineStart = -1;
                    HexExpectedLength = -1;
                }
            }

            return remove;
        }

        /* Drawing helpers.  */

        /* Append TEXT at the end in COLOR.  This moves the selection; callers
           save and restore the user's selection.  */
        private void AppendRaw(string text, Color color)
        {
            RichTextBox.SelectionStart = RichTextBox.TextLength;
            RichTextBox.SelectionLength = 0;
            RichTextBox.SelectionColor = color;
            RichTextBox.AppendText(text);
        }

        /* Mix colors A and B: AMOUNT 1 gives A, 0 gives B.  */
        private static Color Blend(Color a, Color b, double amount)
        {
            return Color.FromArgb(
                (int)(a.R * amount + b.R * (1 - amount)),
                (int)(a.G * amount + b.G * (1 - amount)),
                (int)(a.B * amount + b.B * (1 - amount)));
        }

        /* Return white for a dark BACKGROUND and black for a light one.  */
        private static Color ForeColorFor(Color background) =>
            background.GetBrightness() < 0.5f ? Color.White : Color.Black;

        /* The Win32 SendMessage function, used for WM_SETREDRAW.  */
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /* Suspend painting of the text box while ENABLED is false, which makes
           many small appends much faster, and repaint when it is true
           again.  */
        private void SetRedraw(bool enabled)
        {
            if (!RichTextBox.IsHandleCreated)
                return;

            SendMessage(RichTextBox.Handle, 0x000B /* WM_SETREDRAW */, (IntPtr)(enabled ? 1 : 0), IntPtr.Zero);
            if (enabled)
                RichTextBox.Invalidate();
        }
    }
}
