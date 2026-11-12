using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace BrightnessControl {
    internal static class BalloonTooltip {
        private static readonly BalloonForm tooltip = new BalloonForm();
        private static readonly Timer hideTimer = new Timer();

        static BalloonTooltip() {
            hideTimer.Tick += delegate(object s, EventArgs e) { Hide(); };
            tooltip.MouseDown += delegate(object s, MouseEventArgs e) { Hide(); };
        }

        public static void Show(string message, Bitmap icon, string note, int timeoutMs) {
            if (tooltip.InvokeRequired) {
                tooltip.BeginInvoke(new Action(delegate() { Show(message, icon, note, timeoutMs); }));
                return;
            }

            hideTimer.Stop();
            tooltip.LostFocus -= OnLostFocus;
            tooltip.SetContent(message, icon, note);
            tooltip.ShowInCorner();

            if (timeoutMs > 0) {
                hideTimer.Interval = timeoutMs;
                hideTimer.Start();
            } else {
                tooltip.Focus();
                tooltip.LostFocus += OnLostFocus;
            }
        }

        public static void Hide() {
            if (tooltip.InvokeRequired) {
                tooltip.BeginInvoke(new Action(Hide));
                return;
            }
            hideTimer.Stop();
            tooltip.LostFocus -= OnLostFocus;
            tooltip.Hide();
        }

        private static void OnLostFocus(object sender, EventArgs e) {
            if (!hideTimer.Enabled) Hide();
        }

        private sealed class BalloonForm : Form {
            private Bitmap iconBitmap;
            private string messageText;
            private string noteText;

            public BalloonForm() {
                BackColor = Color.FromArgb(36, 36, 36);
                ClientSize = new Size(340, 92);
                StartPosition = FormStartPosition.Manual;
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;
                DoubleBuffered = true;
            }

            protected override CreateParams CreateParams {
                get {
                    CreateParams cp = base.CreateParams;
                    cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW
                    cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                    return cp;
                }
            }

            public void SetContent(string message, Bitmap icon, string note) {
                messageText = message;
                noteText = note;
                if (iconBitmap != null) {
                    iconBitmap.Dispose();
                    iconBitmap = null;
                }
                if (icon != null) {
                    iconBitmap = new Bitmap(icon);
                }
            }

            public void ShowInCorner() {
                Interop.TryEnableRoundedCorners(Handle);
                Rectangle wa = Screen.GetWorkingArea(this);
                Location = new Point(wa.Right - 20 - Width, wa.Bottom - 16 - Height);
                Interop.ShowWindow(Handle, Interop.SW_SHOWNOACTIVATE);
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e) {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                using (Pen borderPen = new Pen(Color.FromArgb(70, 70, 70), 1f)) {
                    g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
                }

                int pad = 16;
                int x = pad;
                if (iconBitmap != null) {
                    int iconSize = Height - pad * 2;
                    g.DrawImage(iconBitmap, new Rectangle(x, pad, iconSize, iconSize));
                    x += iconSize + 14;
                }

                Rectangle textArea = new Rectangle(x, pad, Width - x - pad, Height - pad * 2);
                using (Font msgFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                using (Font noteFont = new Font("Segoe UI", 9f, FontStyle.Regular))
                using (StringFormat sf = new StringFormat()) {
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    if (string.IsNullOrEmpty(noteText)) {
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString(messageText ?? "", msgFont, Brushes.White, textArea, sf);
                    } else {
                        SizeF msgSize = g.MeasureString(messageText ?? "", msgFont, textArea.Width, sf);
                        g.DrawString(messageText ?? "", msgFont, Brushes.White, textArea, sf);
                        Rectangle noteArea = new Rectangle(textArea.X, textArea.Y + (int)Math.Ceiling(msgSize.Height) + 2, textArea.Width, textArea.Height - (int)Math.Ceiling(msgSize.Height) - 2);
                        using (SolidBrush muted = new SolidBrush(Color.FromArgb(205, 205, 205))) {
                            g.DrawString(noteText, noteFont, muted, noteArea, sf);
                        }
                    }
                }
            }

            protected override void Dispose(bool disposing) {
                if (disposing && iconBitmap != null) {
                    iconBitmap.Dispose();
                    iconBitmap = null;
                }
                base.Dispose(disposing);
            }
        }
    }
}

