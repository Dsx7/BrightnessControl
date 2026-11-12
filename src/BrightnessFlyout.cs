using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrightnessControl {
    internal sealed class BrightnessFlyout : Form {
        private readonly SmoothSlider slider;
        private readonly Label titleLabel;
        private readonly PictureBox iconBox;
        private readonly Button minusBtn;
        private readonly Label percentLabel;
        private readonly TextBox percentInput;
        private readonly Button plusBtn;
        private readonly Button screenOffBtn;
        private readonly Button setupToggleBtn;

        private readonly Panel presetsPanel;
        private readonly List<Button> presetButtons = new List<Button>();

        private readonly Panel setupPanel;
        private readonly Label presetsFieldLabel;
        private readonly TextBox presetsBox;
        private readonly Button savePresetsBtn;
        private readonly Button resetPresetsBtn;
        private readonly Label stepFieldLabel;
        private readonly ComboBox stepCombo;
        private readonly CheckBox showPresetsCheck;
        private readonly CheckBox startupCheck;

        private readonly ToolTip toolTip = new ToolTip();

        private bool isLightTheme;
        private bool setupExpanded;
        private bool updatingControls;
        private int currentBrightness = 50;
        private int lastHighlightedBrightness = -1;
        private float dpiScale = 1f;

        public DateTime LastDeactivatedUtc { get; private set; }

        public event Action<int> BrightnessRequested;
        public event Action DragCompleted;
        public event Action TurnOffScreenRequested;
        public event Action SettingsChanged;

        public BrightnessFlyout() {
            LastDeactivatedUtc = DateTime.MinValue;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            KeyPreview = true;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            using (Graphics g = CreateGraphics()) {
                dpiScale = g.DpiX / 96f;
            }

            iconBox = new PictureBox();
            iconBox.SizeMode = PictureBoxSizeMode.Zoom;
            iconBox.BackColor = Color.Transparent;

            titleLabel = new Label();
            titleLabel.Text = "Brightness";
            titleLabel.AutoSize = false;
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.Font = new Font("Segoe UI", 10f, FontStyle.Bold);

            minusBtn = CreateFlatButton("-");
            minusBtn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            minusBtn.Click += delegate(object s, EventArgs e) { SetAndEmit(currentBrightness - 1); };
            toolTip.SetToolTip(minusBtn, "Decrease 1%");

            percentLabel = new Label();
            percentLabel.Text = "50%";
            percentLabel.TextAlign = ContentAlignment.MiddleCenter;
            percentLabel.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            percentLabel.Cursor = Cursors.IBeam;
            percentLabel.Click += delegate(object s, EventArgs e) { BeginInlinePercentEdit(); };
            toolTip.SetToolTip(percentLabel, "Click to type 0-100");

            percentInput = new TextBox();
            percentInput.Visible = false;
            percentInput.BorderStyle = BorderStyle.FixedSingle;
            percentInput.TextAlign = HorizontalAlignment.Center;
            percentInput.MaxLength = 3;
            percentInput.KeyDown += OnPercentInputKeyDown;
            percentInput.LostFocus += delegate(object s, EventArgs e) { CommitInlinePercentEdit(true); };

            plusBtn = CreateFlatButton("+");
            plusBtn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            plusBtn.Click += delegate(object s, EventArgs e) { SetAndEmit(currentBrightness + 1); };
            toolTip.SetToolTip(plusBtn, "Increase 1%");

            screenOffBtn = CreateFlatButton("Off");
            screenOffBtn.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            screenOffBtn.Click += delegate(object s, EventArgs e) {
                Hide();
                Action h = TurnOffScreenRequested;
                if (h != null) h();
            };
            toolTip.SetToolTip(screenOffBtn, "Turn off screen");

            setupToggleBtn = CreateFlatButton("Setup");
            setupToggleBtn.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            setupToggleBtn.Click += delegate(object s, EventArgs e) {
                setupExpanded = !setupExpanded;
                LayoutAndResize();
            };
            toolTip.SetToolTip(setupToggleBtn, "Configure presets, wheel step, and startup");

            slider = new SmoothSlider();
            slider.ValueChanged += delegate(int val) {
                currentBrightness = val;
                UpdateValueVisuals();
                Action<int> h = BrightnessRequested;
                if (h != null) h(val);
            };
            slider.DragCompleted += delegate() {
                Action h = DragCompleted;
                if (h != null) h();
            };

            presetsPanel = new Panel();
            presetsPanel.BackColor = Color.Transparent;

            setupPanel = new Panel();
            setupPanel.BackColor = Color.Transparent;
            setupPanel.Visible = false;

            presetsFieldLabel = new Label();
            presetsFieldLabel.Text = "Quick Presets (0-100):";
            presetsFieldLabel.AutoSize = false;
            presetsFieldLabel.TextAlign = ContentAlignment.MiddleLeft;
            presetsFieldLabel.Font = new Font("Segoe UI", 8.75f, FontStyle.Regular);

            presetsBox = new TextBox();
            presetsBox.BorderStyle = BorderStyle.FixedSingle;
            presetsBox.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            presetsBox.KeyDown += delegate(object s, KeyEventArgs e) {
                if (e.KeyCode == Keys.Enter) {
                    e.SuppressKeyPress = true;
                    ApplyPresetInput();
                }
            };

            savePresetsBtn = CreateFlatButton("Save");
            savePresetsBtn.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            savePresetsBtn.Click += delegate(object s, EventArgs e) { ApplyPresetInput(); };

            resetPresetsBtn = CreateFlatButton("Reset");
            resetPresetsBtn.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            resetPresetsBtn.Click += delegate(object s, EventArgs e) {
                SettingsStore.ResetPresets();
                presetsBox.Text = SettingsStore.FormatPresets(SettingsStore.Presets);
                RebuildPresetButtons();
                LayoutAndResize();
                NotifySettingsChanged();
            };

            stepFieldLabel = new Label();
            stepFieldLabel.Text = "Tray Wheel Step:";
            stepFieldLabel.AutoSize = false;
            stepFieldLabel.TextAlign = ContentAlignment.MiddleLeft;
            stepFieldLabel.Font = new Font("Segoe UI", 8.75f, FontStyle.Regular);

            stepCombo = new ComboBox();
            stepCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            stepCombo.FlatStyle = FlatStyle.Flat;
            stepCombo.Items.AddRange(new object[] { "1%", "2%", "5%", "10%", "15%", "20%" });
            stepCombo.SelectedIndexChanged += delegate(object s, EventArgs e) {
                if (updatingControls || stepCombo.SelectedItem == null) return;
                int val;
                if (int.TryParse(stepCombo.SelectedItem.ToString().TrimEnd('%'), out val)) {
                    SettingsStore.WheelStep = val;
                    NotifySettingsChanged();
                }
            };

            showPresetsCheck = new CheckBox();
            showPresetsCheck.Text = "Show quick presets";
            showPresetsCheck.AutoSize = false;
            showPresetsCheck.Font = new Font("Segoe UI", 8.75f, FontStyle.Regular);
            showPresetsCheck.CheckedChanged += delegate(object s, EventArgs e) {
                if (updatingControls) return;
                SettingsStore.ShowPresets = showPresetsCheck.Checked;
                LayoutAndResize();
                NotifySettingsChanged();
            };

            startupCheck = new CheckBox();
            startupCheck.Text = "Start with Windows";
            startupCheck.AutoSize = false;
            startupCheck.Font = new Font("Segoe UI", 8.75f, FontStyle.Regular);
            startupCheck.CheckedChanged += delegate(object s, EventArgs e) {
                if (updatingControls) return;
                bool result = SettingsStore.SetStartup(startupCheck.Checked);
                if (result != startupCheck.Checked) {
                    updatingControls = true;
                    startupCheck.Checked = result;
                    updatingControls = false;
                }
                NotifySettingsChanged();
            };

            setupPanel.Controls.AddRange(new Control[] {
                presetsFieldLabel, presetsBox, savePresetsBtn, resetPresetsBtn,
                stepFieldLabel, stepCombo, showPresetsCheck, startupCheck
            });

            Controls.AddRange(new Control[] {
                iconBox, titleLabel, minusBtn, percentLabel, percentInput, plusBtn,
                screenOffBtn, setupToggleBtn, slider, presetsPanel, setupPanel
            });
        }

        protected override CreateParams CreateParams {
            get {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                return cp;
            }
        }

        public bool IsUserDragging {
            get { return slider.IsDragging; }
        }

        public void ShowFlyout(Rectangle iconBounds, int currentValue, bool expandSetup) {
            currentBrightness = Math.Max(0, Math.Min(100, currentValue));
            setupExpanded = expandSetup;
            RefreshTheme();
            LoadSettingsToControls();
            RebuildPresetButtons();
            slider.SetImmediate(currentBrightness);
            lastHighlightedBrightness = -1;
            UpdateValueVisuals();
            LayoutAndResize();
            PositionNearTray(iconBounds);

            if (!Visible) {
                Show();
            }
            Interop.TryEnableRoundedCorners(Handle);
            Activate();
            slider.Focus();
        }

        public void UpdateBrightnessExternal(int value) {
            if (slider.IsDragging || percentInput.Visible) return;
            currentBrightness = Math.Max(0, Math.Min(100, value));
            UpdateValueVisuals();
        }

        public void RefreshTheme() {
            isLightTheme = SettingsStore.IsWindowsInLightMode();
            slider.IsLightTheme = isLightTheme;

            Color bg = isLightTheme ? Color.FromArgb(248, 249, 251) : Color.FromArgb(32, 32, 34);
            Color fg = isLightTheme ? Color.FromArgb(24, 24, 27) : Color.FromArgb(244, 244, 246);
            Color muted = isLightTheme ? Color.FromArgb(90, 95, 105) : Color.FromArgb(180, 182, 188);
            Color surface = isLightTheme ? Color.FromArgb(234, 237, 242) : Color.FromArgb(48, 48, 52);
            Color surfaceHover = isLightTheme ? Color.FromArgb(220, 224, 232) : Color.FromArgb(64, 64, 70);
            Color inputBg = isLightTheme ? Color.White : Color.FromArgb(42, 42, 46);
            Color accent = isLightTheme ? Color.FromArgb(0, 103, 192) : Color.FromArgb(76, 194, 255);

            BackColor = bg;
            ForeColor = fg;
            titleLabel.ForeColor = fg;
            percentLabel.ForeColor = accent;
            presetsFieldLabel.ForeColor = muted;
            stepFieldLabel.ForeColor = muted;
            showPresetsCheck.ForeColor = fg;
            startupCheck.ForeColor = fg;

            percentInput.BackColor = inputBg;
            percentInput.ForeColor = fg;
            presetsBox.BackColor = inputBg;
            presetsBox.ForeColor = fg;
            stepCombo.BackColor = inputBg;
            stepCombo.ForeColor = fg;

            StyleButton(minusBtn, surface, surfaceHover, fg);
            StyleButton(plusBtn, surface, surfaceHover, fg);
            StyleButton(screenOffBtn, surface, surfaceHover, fg);
            StyleButton(setupToggleBtn, setupExpanded ? accent : surface, setupExpanded ? accent : surfaceHover, setupExpanded ? (isLightTheme ? Color.White : Color.FromArgb(18, 18, 20)) : fg);
            StyleButton(savePresetsBtn, accent, accent, isLightTheme ? Color.White : Color.FromArgb(18, 18, 20));
            StyleButton(resetPresetsBtn, surface, surfaceHover, fg);

            foreach (Button btn in presetButtons) {
                StylePresetButton(btn);
            }

            if (iconBox.Image != null) {
                iconBox.Image.Dispose();
                iconBox.Image = null;
            }
            string res = isLightTheme ? "BrightnessControl.Resources.IconLight.png" : "BrightnessControl.Resources.IconDark.png";
            iconBox.Image = IconRenderer.GetResourceBitmap(res);

            Invalidate();
        }

        private int S(int px) {
            return (int)Math.Round(px * dpiScale);
        }

        private void LayoutAndResize() {
            int pad = S(14);
            int width = S(336);
            int innerW = width - pad * 2;

            // Header row
            int y = pad;
            int rowH = S(26);
            iconBox.Bounds = new Rectangle(pad, y + S(3), S(20), S(20));
            titleLabel.Bounds = new Rectangle(pad + S(26), y, S(86), rowH);

            int right = width - pad;
            int setupW = S(48);
            int offW = S(34);
            int btnSz = S(24);
            int pctW = S(44);

            setupToggleBtn.Bounds = new Rectangle(right - setupW, y + S(1), setupW, btnSz);
            right -= setupW + S(4);
            screenOffBtn.Bounds = new Rectangle(right - offW, y + S(1), offW, btnSz);
            right -= offW + S(6);
            plusBtn.Bounds = new Rectangle(right - btnSz, y + S(1), btnSz, btnSz);
            right -= btnSz + S(2);
            percentLabel.Bounds = new Rectangle(right - pctW, y + S(1), pctW, btnSz);
            percentInput.Bounds = new Rectangle(right - pctW + S(2), y + S(2), pctW - S(4), btnSz - S(2));
            right -= pctW + S(2);
            minusBtn.Bounds = new Rectangle(right - btnSz, y + S(1), btnSz, btnSz);

            y += rowH + S(8);

            // Slider row
            slider.Bounds = new Rectangle(pad, y, innerW, S(34));
            y += S(34) + S(6);

            // Quick presets row
            bool showPresets = SettingsStore.ShowPresets && presetButtons.Count > 0;
            presetsPanel.Visible = showPresets;
            if (showPresets) {
                int pH = S(26);
                presetsPanel.Bounds = new Rectangle(pad, y, innerW, pH);
                int count = presetButtons.Count;
                int gap = S(6);
                int btnW = (innerW - gap * (count - 1)) / count;
                int x = 0;
                for (int i = 0; i < count; i++) {
                    int w = (i == count - 1) ? (innerW - x) : btnW;
                    presetButtons[i].Bounds = new Rectangle(x, 0, w, pH);
                    x += w + gap;
                }
                y += pH + S(8);
            }

            // Expandable setup panel
            setupPanel.Visible = setupExpanded;
            Color accent = isLightTheme ? Color.FromArgb(0, 103, 192) : Color.FromArgb(76, 194, 255);
            Color surface = isLightTheme ? Color.FromArgb(234, 237, 242) : Color.FromArgb(48, 48, 52);
            Color surfaceHover = isLightTheme ? Color.FromArgb(220, 224, 232) : Color.FromArgb(64, 64, 70);
            Color fg = isLightTheme ? Color.FromArgb(24, 24, 27) : Color.FromArgb(244, 244, 246);
            StyleButton(setupToggleBtn, setupExpanded ? accent : surface, setupExpanded ? accent : surfaceHover, setupExpanded ? (isLightTheme ? Color.White : Color.FromArgb(18, 18, 20)) : fg);

            if (setupExpanded) {
                int sy = S(10);
                presetsFieldLabel.Bounds = new Rectangle(0, sy, innerW, S(18));
                sy += S(20);

                int resetW = S(48);
                int saveW = S(46);
                int boxW = innerW - saveW - resetW - S(8);
                presetsBox.Bounds = new Rectangle(0, sy, boxW, S(24));
                savePresetsBtn.Bounds = new Rectangle(boxW + S(4), sy, saveW, S(24));
                resetPresetsBtn.Bounds = new Rectangle(boxW + saveW + S(8), sy, resetW, S(24));
                sy += S(32);

                int comboW = S(76);
                stepFieldLabel.Bounds = new Rectangle(0, sy, innerW - comboW - S(8), S(24));
                stepCombo.Bounds = new Rectangle(innerW - comboW, sy, comboW, S(24));
                sy += S(30);

                int halfW = (innerW - S(8)) / 2;
                showPresetsCheck.Bounds = new Rectangle(0, sy, halfW, S(22));
                startupCheck.Bounds = new Rectangle(halfW + S(8), sy, innerW - halfW - S(8), S(22));
                sy += S(26);

                setupPanel.Bounds = new Rectangle(pad, y, innerW, sy);
                y += sy + S(10);
            } else {
                y += S(6);
            }

            Rectangle oldBounds = Bounds;
            ClientSize = new Size(width, y);
            if (Visible && oldBounds.Height > 0 && oldBounds.Height != Height) {
                // Keep bottom edge anchored when toggling setup section
                Top = oldBounds.Bottom - Height;
                Rectangle wa = Screen.FromControl(this).WorkingArea;
                if (Top < wa.Top + S(8)) Top = wa.Top + S(8);
                if (Bottom > wa.Bottom - S(8)) Top = wa.Bottom - S(8) - Height;
            }
            Invalidate();
        }

        private void PositionNearTray(Rectangle iconBounds) {
            Rectangle wa;
            int x;
            int y;
            int margin = S(10);

            if (!iconBounds.IsEmpty) {
                Point center = new Point(iconBounds.Left + iconBounds.Width / 2, iconBounds.Top + iconBounds.Height / 2);
                wa = Screen.FromPoint(center).WorkingArea;
                x = center.X - Width / 2;
                if (center.Y >= wa.Bottom - S(20)) {
                    y = wa.Bottom - margin - Height;
                } else if (center.Y <= wa.Top + S(20)) {
                    y = wa.Top + margin;
                } else {
                    y = iconBounds.Top - margin - Height;
                }
            } else {
                wa = Screen.FromPoint(Cursor.Position).WorkingArea;
                x = wa.Right - margin - Width;
                y = wa.Bottom - margin - Height;
            }

            if (x < wa.Left + margin) x = wa.Left + margin;
            if (x + Width > wa.Right - margin) x = wa.Right - margin - Width;
            if (y < wa.Top + margin) y = wa.Top + margin;
            if (y + Height > wa.Bottom - margin) y = wa.Bottom - margin - Height;

            Location = new Point(x, y);
        }

        private void LoadSettingsToControls() {
            updatingControls = true;
            try {
                presetsBox.Text = SettingsStore.FormatPresets(SettingsStore.Presets);
                showPresetsCheck.Checked = SettingsStore.ShowPresets;
                startupCheck.Checked = SettingsStore.StartsWithWindows();

                string stepText = SettingsStore.WheelStep + "%";
                int idx = stepCombo.Items.IndexOf(stepText);
                if (idx < 0) {
                    stepCombo.Items.Add(stepText);
                    idx = stepCombo.Items.IndexOf(stepText);
                }
                stepCombo.SelectedIndex = idx;
            } finally {
                updatingControls = false;
            }
        }

        private void RebuildPresetButtons() {
            foreach (Button old in presetButtons) {
                presetsPanel.Controls.Remove(old);
                old.Dispose();
            }
            presetButtons.Clear();

            int[] presets = SettingsStore.Presets;
            foreach (int p in presets) {
                int val = p;
                Button btn = CreateFlatButton(val + "%");
                btn.Tag = val;
                btn.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                btn.Click += delegate(object s, EventArgs e) { SetAndEmit(val); };
                StylePresetButton(btn);
                presetButtons.Add(btn);
                presetsPanel.Controls.Add(btn);
            }
        }

        private void UpdateValueVisuals() {
            if (!slider.IsDragging) {
                slider.Value = currentBrightness;
            }
            string txt = currentBrightness + "%";
            if (percentLabel.Text != txt) {
                percentLabel.Text = txt;
            }
            if (lastHighlightedBrightness != currentBrightness) {
                int prev = lastHighlightedBrightness;
                lastHighlightedBrightness = currentBrightness;
                foreach (Button btn in presetButtons) {
                    int val = (btn.Tag is int) ? (int)btn.Tag : -1;
                    if (val == prev || val == currentBrightness) {
                        StylePresetButton(btn);
                    }
                }
            }
        }

        private void SetAndEmit(int target) {
            int clamped = Math.Max(0, Math.Min(100, target));
            currentBrightness = clamped;
            UpdateValueVisuals();
            Action<int> h = BrightnessRequested;
            if (h != null) h(clamped);
        }

        private void BeginInlinePercentEdit() {
            percentInput.Text = currentBrightness.ToString();
            percentLabel.Visible = false;
            percentInput.Visible = true;
            percentInput.Focus();
            percentInput.SelectAll();
        }

        private void CommitInlinePercentEdit(bool apply) {
            if (!percentInput.Visible) return;
            string txt = percentInput.Text;
            percentInput.Visible = false;
            percentLabel.Visible = true;
            if (apply) {
                int val;
                if (int.TryParse(txt.Trim().TrimEnd('%'), out val)) {
                    SetAndEmit(val);
                }
            }
        }

        private void OnPercentInputKeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Enter) {
                e.SuppressKeyPress = true;
                CommitInlinePercentEdit(true);
                slider.Focus();
            } else if (e.KeyCode == Keys.Escape) {
                e.SuppressKeyPress = true;
                CommitInlinePercentEdit(false);
                slider.Focus();
            }
        }

        private void ApplyPresetInput() {
            int[] parsed = SettingsStore.ParsePresets(presetsBox.Text);
            if (parsed.Length == 0) {
                SettingsStore.ResetPresets();
                parsed = SettingsStore.Presets;
            } else {
                SettingsStore.Presets = parsed;
            }
            presetsBox.Text = SettingsStore.FormatPresets(parsed);
            RebuildPresetButtons();
            LayoutAndResize();
            NotifySettingsChanged();
        }

        private void NotifySettingsChanged() {
            Action h = SettingsChanged;
            if (h != null) h();
        }

        private Button CreateFlatButton(string text) {
            Button btn = new Button();
            btn.Text = text;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.TabStop = false;
            btn.Cursor = Cursors.Hand;
            return btn;
        }

        private static void StyleButton(Button btn, Color bg, Color hover, Color fg) {
            btn.BackColor = bg;
            btn.ForeColor = fg;
            btn.FlatAppearance.MouseOverBackColor = hover;
            btn.FlatAppearance.MouseDownBackColor = hover;
        }

        private void StylePresetButton(Button btn) {
            int val = (btn.Tag is int) ? (int)btn.Tag : -1;
            bool active = (val == currentBrightness);
            Color accent = isLightTheme ? Color.FromArgb(0, 103, 192) : Color.FromArgb(76, 194, 255);
            Color surface = isLightTheme ? Color.FromArgb(234, 237, 242) : Color.FromArgb(48, 48, 52);
            Color hover = isLightTheme ? Color.FromArgb(218, 222, 230) : Color.FromArgb(64, 64, 70);
            Color fg = isLightTheme ? Color.FromArgb(24, 24, 27) : Color.FromArgb(244, 244, 246);
            Color activeFg = isLightTheme ? Color.White : Color.FromArgb(18, 18, 20);
            StyleButton(btn, active ? accent : surface, active ? accent : hover, active ? activeFg : fg);
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape && !percentInput.Visible) {
                Hide();
                e.Handled = true;
            }
        }

        protected override void OnDeactivate(EventArgs e) {
            base.OnDeactivate(e);
            LastDeactivatedUtc = DateTime.UtcNow;
            CommitInlinePercentEdit(true);
            Hide();
        }

        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            Color border = isLightTheme ? Color.FromArgb(208, 212, 220) : Color.FromArgb(66, 66, 72);
            using (Pen p = new Pen(border, 1f)) {
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            }
            if (setupExpanded && setupPanel.Visible) {
                int lineY = setupPanel.Top + S(2);
                using (Pen div = new Pen(border, 1f)) {
                    e.Graphics.DrawLine(div, S(14), lineY, Width - S(14), lineY);
                }
            }
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                toolTip.Dispose();
                if (iconBox.Image != null) {
                    iconBox.Image.Dispose();
                    iconBox.Image = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}

