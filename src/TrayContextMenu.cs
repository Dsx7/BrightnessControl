using System;
using System.Drawing;
using System.Windows.Forms;

namespace BrightnessControl {
    internal sealed class TrayContextMenu : ContextMenuStrip {
        private readonly Panel sliderHostPanel;
        private readonly Label headerTitleLabel;
        private readonly Label headerPercentLabel;
        private readonly SmoothSlider menuSlider;
        private readonly ToolStripControlHost sliderHost;

        private readonly ToolStripMenuItem openPopupItem;
        private readonly ToolStripMenuItem presetsMenuItem;
        private readonly ToolStripMenuItem turnOffItem;
        private readonly ToolStripMenuItem startupItem;
        private readonly ToolStripMenuItem setupItem;
        private readonly ToolStripMenuItem aboutItem;
        private readonly ToolStripMenuItem exitItem;

        private bool isLightTheme;
        private int currentBrightness = 50;

        public event Action<int> BrightnessRequested;
        public event Action DragCompleted;
        public event Action OpenPopupRequested;
        public event Action OpenSetupRequested;
        public event Action TurnOffScreenRequested;
        public event Action AboutRequested;
        public event Action ExitRequested;

        public TrayContextMenu() : this(SettingsStore.LastBrightness) {
        }

        public TrayContextMenu(int initialBrightness) {
            currentBrightness = Math.Max(0, Math.Min(100, initialBrightness));
            ShowImageMargin = false;
            ShowCheckMargin = true;
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);

            float scale = 1f;
            using (Graphics g = CreateGraphics()) {
                scale = g.DpiX / 96f;
            }
            int w = (int)Math.Round(230 * scale);
            int h = (int)Math.Round(56 * scale);
            int pad = (int)Math.Round(10 * scale);

            sliderHostPanel = new Panel();
            sliderHostPanel.Size = new Size(w, h);
            sliderHostPanel.Padding = new Padding(0);

            headerTitleLabel = new Label();
            headerTitleLabel.Text = "Brightness (0-100%)";
            headerTitleLabel.Bounds = new Rectangle(pad, (int)Math.Round(4 * scale), w - pad * 2 - (int)Math.Round(48 * scale), (int)Math.Round(18 * scale));
            headerTitleLabel.Font = new Font("Segoe UI", 8.75f, FontStyle.Bold);
            headerTitleLabel.TextAlign = ContentAlignment.MiddleLeft;

            headerPercentLabel = new Label();
            headerPercentLabel.Text = currentBrightness + "%";
            headerPercentLabel.Bounds = new Rectangle(w - pad - (int)Math.Round(48 * scale), (int)Math.Round(4 * scale), (int)Math.Round(48 * scale), (int)Math.Round(18 * scale));
            headerPercentLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            headerPercentLabel.TextAlign = ContentAlignment.MiddleRight;

            menuSlider = new SmoothSlider(currentBrightness);
            menuSlider.Bounds = new Rectangle(pad - (int)Math.Round(4 * scale), (int)Math.Round(24 * scale), w - (pad - (int)Math.Round(4 * scale)) * 2, (int)Math.Round(28 * scale));
            menuSlider.ValueChanged += delegate(int val) {
                currentBrightness = val;
                headerPercentLabel.Text = val + "%";
                Action<int> handler = BrightnessRequested;
                if (handler != null) handler(val);
            };
            menuSlider.DragCompleted += delegate() {
                Action hnd = DragCompleted;
                if (hnd != null) hnd();
            };

            sliderHostPanel.Controls.Add(headerTitleLabel);
            sliderHostPanel.Controls.Add(headerPercentLabel);
            sliderHostPanel.Controls.Add(menuSlider);

            sliderHost = new ToolStripControlHost(sliderHostPanel);
            sliderHost.AutoSize = false;
            sliderHost.Size = sliderHostPanel.Size;
            sliderHost.Margin = Padding.Empty;
            sliderHost.Padding = Padding.Empty;

            openPopupItem = new ToolStripMenuItem("Open Brightness Slider...");
            openPopupItem.Font = new Font(Font, FontStyle.Bold);
            openPopupItem.Click += delegate(object s, EventArgs e) {
                Action hnd = OpenPopupRequested;
                if (hnd != null) hnd();
            };

            presetsMenuItem = new ToolStripMenuItem("Quick Presets");

            turnOffItem = new ToolStripMenuItem("Turn off screen");
            turnOffItem.Click += delegate(object s, EventArgs e) {
                Action hnd = TurnOffScreenRequested;
                if (hnd != null) hnd();
            };

            startupItem = new ToolStripMenuItem("Start with Windows");
            startupItem.CheckOnClick = false;
            startupItem.Click += delegate(object s, EventArgs e) {
                bool next = !SettingsStore.StartsWithWindows();
                startupItem.Checked = SettingsStore.SetStartup(next);
            };

            setupItem = new ToolStripMenuItem("Setup / Settings...");
            setupItem.Click += delegate(object s, EventArgs e) {
                Action hnd = OpenSetupRequested;
                if (hnd != null) hnd();
            };

            aboutItem = new ToolStripMenuItem("About...");
            aboutItem.Click += delegate(object s, EventArgs e) {
                Action hnd = AboutRequested;
                if (hnd != null) hnd();
            };

            exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += delegate(object s, EventArgs e) {
                Action hnd = ExitRequested;
                if (hnd != null) hnd();
            };

            Items.Add(sliderHost);
            Items.Add(new ToolStripSeparator());
            Items.Add(openPopupItem);
            Items.Add(presetsMenuItem);
            Items.Add(new ToolStripSeparator());
            Items.Add(turnOffItem);
            Items.Add(startupItem);
            Items.Add(setupItem);
            Items.Add(aboutItem);
            Items.Add(new ToolStripSeparator());
            Items.Add(exitItem);

            Opening += OnMenuOpening;
            ApplyTheme();
        }

        public bool IsUserDragging {
            get { return menuSlider.IsDragging; }
        }

        public void SyncBrightness(int value) {
            if (menuSlider.IsDragging) return;
            currentBrightness = Math.Max(0, Math.Min(100, value));
            menuSlider.SetImmediate(currentBrightness);
            string txt = currentBrightness + "%";
            if (headerPercentLabel.Text != txt) {
                headerPercentLabel.Text = txt;
            }
        }

        public void ApplyTheme() {
            isLightTheme = SettingsStore.IsWindowsInLightMode();
            menuSlider.IsLightTheme = isLightTheme;

            Color bg = isLightTheme ? Color.FromArgb(248, 249, 251) : Color.FromArgb(36, 36, 38);
            Color fg = isLightTheme ? Color.FromArgb(24, 24, 27) : Color.FromArgb(240, 240, 244);
            Color accent = isLightTheme ? Color.FromArgb(0, 103, 192) : Color.FromArgb(76, 194, 255);

            BackColor = bg;
            ForeColor = fg;
            sliderHostPanel.BackColor = bg;
            headerTitleLabel.ForeColor = fg;
            headerPercentLabel.ForeColor = accent;

            Renderer = new ThemedMenuRenderer(isLightTheme);
        }

        private void OnMenuOpening(object sender, System.ComponentModel.CancelEventArgs e) {
            ApplyTheme();
            startupItem.Checked = SettingsStore.StartsWithWindows();

            presetsMenuItem.DropDownItems.Clear();
            int[] presets = SettingsStore.Presets;
            for (int i = presets.Length - 1; i >= 0; i--) {
                int level = presets[i];
                ToolStripMenuItem item = new ToolStripMenuItem(level + "%");
                item.Checked = (level == currentBrightness);
                item.Click += delegate(object s2, EventArgs e2) {
                    SyncBrightness(level);
                    Action<int> h = BrightnessRequested;
                    if (h != null) h(level);
                };
                presetsMenuItem.DropDownItems.Add(item);
            }
        }

        private sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer {
            private readonly bool light;

            public ThemedMenuRenderer(bool light) : base(new ThemedColorTable(light)) {
                this.light = light;
                RoundedEdges = false;
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
                e.TextColor = light ? Color.FromArgb(24, 24, 27) : Color.FromArgb(240, 240, 244);
                base.OnRenderItemText(e);
            }

            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) {
                Rectangle r = e.ImageRectangle;
                Color accent = light ? Color.FromArgb(0, 103, 192) : Color.FromArgb(76, 194, 255);
                using (Pen p = new Pen(accent, 1.8f)) {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    e.Graphics.DrawLines(p, new Point[] {
                        new Point(r.Left + 3, r.Top + r.Height / 2),
                        new Point(r.Left + r.Width / 2 - 1, r.Bottom - 4),
                        new Point(r.Right - 3, r.Top + 4)
                    });
                }
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) {
                e.ArrowColor = light ? Color.FromArgb(80, 80, 90) : Color.FromArgb(200, 200, 210);
                base.OnRenderArrow(e);
            }
        }

        private sealed class ThemedColorTable : ProfessionalColorTable {
            private readonly bool light;
            public ThemedColorTable(bool light) { this.light = light; }

            public override Color ToolStripDropDownBackground {
                get { return light ? Color.FromArgb(248, 249, 251) : Color.FromArgb(36, 36, 38); }
            }
            public override Color MenuBorder {
                get { return light ? Color.FromArgb(208, 212, 220) : Color.FromArgb(66, 66, 72); }
            }
            public override Color MenuItemBorder {
                get { return Color.Transparent; }
            }
            public override Color MenuItemSelected {
                get { return light ? Color.FromArgb(228, 232, 240) : Color.FromArgb(56, 56, 62); }
            }
            public override Color SeparatorDark {
                get { return light ? Color.FromArgb(220, 223, 230) : Color.FromArgb(62, 62, 68); }
            }
            public override Color ImageMarginGradientBegin {
                get { return ToolStripDropDownBackground; }
            }
            public override Color ImageMarginGradientMiddle {
                get { return ToolStripDropDownBackground; }
            }
            public override Color ImageMarginGradientEnd {
                get { return ToolStripDropDownBackground; }
            }
        }
    }
}

