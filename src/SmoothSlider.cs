using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrightnessControl {
    internal sealed class SmoothSlider : Control {
        private int value = 50;
        private float visualValue = 50f;
        private float dpiScale = 1f;
        private bool isDragging;
        private bool isHovering;
        private bool isLightTheme;
        private int wheelStep = 2;
        private readonly Timer animTimer;

        public event Action<int> ValueChanged;
        public event Action DragStarted;
        public event Action DragCompleted;

        public SmoothSlider() {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Selectable,
                true
            );
            Cursor = Cursors.Hand;
            Size = new Size(240, 32);

            animTimer = new Timer();
            animTimer.Interval = 15;
            animTimer.Tick += OnAnimTick;
        }

        public int Value {
            get { return value; }
            set {
                int clamped = Math.Max(0, Math.Min(100, value));
                if (this.value == clamped && Math.Abs(visualValue - clamped) < 0.05f) return;
                this.value = clamped;
                if (isDragging || !Visible) {
                    animTimer.Stop();
                    visualValue = clamped;
                    Invalidate();
                } else {
                    animTimer.Start();
                }
            }
        }

        public void SetImmediate(int val) {
            int clamped = Math.Max(0, Math.Min(100, val));
            animTimer.Stop();
            value = clamped;
            visualValue = clamped;
            Invalidate();
        }

        public bool IsDragging {
            get { return isDragging; }
        }

        public bool IsLightTheme {
            get { return isLightTheme; }
            set {
                if (isLightTheme == value) return;
                isLightTheme = value;
                Invalidate();
            }
        }

        public int WheelStep {
            get { return wheelStep; }
            set { wheelStep = Math.Max(1, Math.Min(25, value)); }
        }

        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            RefreshDpiScale();
        }

        private void RefreshDpiScale() {
            using (Graphics g = CreateGraphics()) {
                dpiScale = g.DpiX / 96f;
            }
        }

        private void OnAnimTick(object sender, EventArgs e) {
            float diff = value - visualValue;
            if (Math.Abs(diff) <= 0.35f || isDragging) {
                visualValue = value;
                animTimer.Stop();
            } else {
                visualValue += diff * 0.38f;
            }
            Invalidate();
        }

        private RectangleF GetTrackBounds() {
            float padX = 11f * dpiScale;
            float trackH = 5f * dpiScale;
            float w = Math.Max(20f, Width - padX * 2f);
            float y = (Height - trackH) / 2f;
            return new RectangleF(padX, y, w, trackH);
        }

        private void SetValueFromMouseX(int mouseX, bool immediateVisual) {
            RectangleF track = GetTrackBounds();
            float ratio = (mouseX - track.Left) / track.Width;
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;

            float exact = ratio * 100f;
            int next = (int)Math.Round(exact);

            if (immediateVisual) {
                animTimer.Stop();
                if (Math.Abs(visualValue - exact) >= 0.05f) {
                    visualValue = exact;
                    Invalidate();
                }
            }

            if (next != value) {
                value = next;
                if (!immediateVisual) {
                    animTimer.Start();
                }
                Action<int> handler = ValueChanged;
                if (handler != null) handler(value);
            }
        }

        private void NudgeValue(int delta) {
            int next = Math.Max(0, Math.Min(100, value + delta));
            if (next != value) {
                value = next;
                animTimer.Start();
                Action<int> handler = ValueChanged;
                if (handler != null) handler(value);
            }
        }

        protected override void OnMouseEnter(EventArgs e) {
            base.OnMouseEnter(e);
            isHovering = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) {
            base.OnMouseLeave(e);
            isHovering = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            isDragging = true;
            Capture = true;
            animTimer.Stop();
            Action start = DragStarted;
            if (start != null) start();
            SetValueFromMouseX(e.X, true);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            if (isDragging) {
                SetValueFromMouseX(e.X, true);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);
            if (isDragging && e.Button == MouseButtons.Left) {
                isDragging = false;
                Capture = false;
                visualValue = value;
                Invalidate();
                Action done = DragCompleted;
                if (done != null) done();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e) {
            base.OnMouseWheel(e);
            int dir = Math.Sign(e.Delta);
            if (dir != 0) {
                NudgeValue(dir * wheelStep);
            }
        }

        protected override bool IsInputKey(Keys keyData) {
            switch (keyData) {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Home:
                case Keys.End:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);
            switch (e.KeyCode) {
                case Keys.Left:
                case Keys.Down:
                    NudgeValue(-1);
                    e.Handled = true;
                    break;
                case Keys.Right:
                case Keys.Up:
                    NudgeValue(1);
                    e.Handled = true;
                    break;
                case Keys.PageDown:
                    NudgeValue(-10);
                    e.Handled = true;
                    break;
                case Keys.PageUp:
                    NudgeValue(10);
                    e.Handled = true;
                    break;
                case Keys.Home:
                    NudgeValue(-100);
                    e.Handled = true;
                    break;
                case Keys.End:
                    NudgeValue(100);
                    e.Handled = true;
                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            RectangleF track = GetTrackBounds();

            Color trackBg = isLightTheme ? Color.FromArgb(208, 211, 217) : Color.FromArgb(82, 82, 86);
            Color accent = isLightTheme ? Color.FromArgb(0, 103, 192) : Color.FromArgb(76, 194, 255);
            Color thumbOuter = isLightTheme ? Color.FromArgb(255, 255, 255) : Color.FromArgb(62, 62, 66);
            Color thumbBorder = isLightTheme ? Color.FromArgb(190, 195, 204) : Color.FromArgb(108, 108, 114);

            using (GraphicsPath bgPath = CreateRoundedRect(track, track.Height / 2f))
            using (SolidBrush bgBrush = new SolidBrush(trackBg)) {
                g.FillPath(bgBrush, bgPath);
            }

            float ratio = Math.Max(0f, Math.Min(1f, visualValue / 100f));
            float thumbX = track.Left + ratio * track.Width;

            float fillW = Math.Max(0f, thumbX - track.Left);
            if (fillW > 1f) {
                RectangleF fillRect = new RectangleF(track.Left, track.Top, fillW, track.Height);
                using (GraphicsPath fillPath = CreateRoundedRect(fillRect, track.Height / 2f))
                using (SolidBrush accentBrush = new SolidBrush(accent)) {
                    g.FillPath(accentBrush, fillPath);
                }
            }

            float outerRadius = (isDragging ? 9.5f : (isHovering ? 9.0f : 8.5f)) * dpiScale;
            float centerY = track.Top + track.Height / 2f;
            RectangleF thumbRect = new RectangleF(
                thumbX - outerRadius,
                centerY - outerRadius,
                outerRadius * 2f,
                outerRadius * 2f
            );

            using (SolidBrush outerBrush = new SolidBrush(thumbOuter))
            using (Pen borderPen = new Pen(thumbBorder, 1f * dpiScale)) {
                g.FillEllipse(outerBrush, thumbRect);
                g.DrawEllipse(borderPen, thumbRect);
            }

            float innerRadius = (isDragging ? 4.2f : (isHovering ? 5.8f : 4.8f)) * dpiScale;
            RectangleF innerRect = new RectangleF(
                thumbX - innerRadius,
                centerY - innerRadius,
                innerRadius * 2f,
                innerRadius * 2f
            );
            using (SolidBrush accentBrush = new SolidBrush(accent)) {
                g.FillEllipse(accentBrush, innerRect);
            }
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                animTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius) {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;
            if (d <= 0.1f) {
                path.AddRectangle(rect);
                return path;
            }
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
