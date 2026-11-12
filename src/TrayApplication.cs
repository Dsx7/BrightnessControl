using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BrightnessControl {
    internal sealed class TrayApplication : Form {
        private readonly NotifyIcon trayIcon;
        private readonly DisplayController display;
        private readonly InputListener input;
        private readonly BrightnessFlyout flyout;
        private readonly TrayContextMenu contextMenu;
        private readonly System.Windows.Forms.Timer themeDebounceTimer;
        private readonly System.Windows.Forms.Timer iconDebounceTimer;
        private readonly EventWaitHandle wakeupEvent;
        private readonly Thread wakeupThread;

        private volatile bool exiting;
        private int currentBrightness;
        private int lastIconBrightness = -1;
        private bool lastIconLightMode;
        private bool hasRenderedIcon;

        public TrayApplication(EventWaitHandle wakeupHandle) {
            wakeupEvent = wakeupHandle;

            Interop.SetProcessDPIAware();

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            Size = new Size(0, 0);
            Opacity = 0;
            CreateHandle();

            display = new DisplayController();
            currentBrightness = display.CurrentValue;
            display.BrightnessChanged += OnExternalBrightnessChanged;

            input = new InputListener();
            input.MouseWheel += OnMouseWheel;
            input.Listen();

            flyout = new BrightnessFlyout();
            flyout.BrightnessRequested += OnUiBrightnessRequested;
            flyout.DragCompleted += FlushTrayVisuals;
            flyout.TurnOffScreenRequested += delegate() { display.TurnOffScreen(); };

            contextMenu = new TrayContextMenu();
            contextMenu.SyncBrightness(currentBrightness);
            contextMenu.BrightnessRequested += OnUiBrightnessRequested;
            contextMenu.DragCompleted += FlushTrayVisuals;
            contextMenu.OpenPopupRequested += delegate() { OpenFlyout(false); };
            contextMenu.OpenSetupRequested += delegate() { OpenFlyout(true); };
            contextMenu.TurnOffScreenRequested += delegate() { display.TurnOffScreen(); };
            contextMenu.AboutRequested += ShowAbout;
            contextMenu.ExitRequested += ExitApp;

            trayIcon = new NotifyIcon();
            trayIcon.ContextMenuStrip = contextMenu;
            trayIcon.MouseDown += OnTrayIconMouseDown;
            trayIcon.MouseUp += OnTrayIconMouseUp;
            trayIcon.Visible = true;

            themeDebounceTimer = new System.Windows.Forms.Timer();
            themeDebounceTimer.Interval = 200;
            themeDebounceTimer.Tick += delegate(object s, EventArgs e) {
                themeDebounceTimer.Stop();
                UpdateTrayVisuals(true);
                if (flyout.Visible) flyout.RefreshTheme();
            };

            iconDebounceTimer = new System.Windows.Forms.Timer();
            iconDebounceTimer.Interval = 80;
            iconDebounceTimer.Tick += delegate(object s, EventArgs e) {
                iconDebounceTimer.Stop();
                UpdateTrayVisuals(false);
            };

            SystemEvents.PaletteChanged += OnSystemChanged;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            UpdateTrayVisuals(true);

            if (wakeupEvent != null) {
                wakeupThread = new Thread(WakeupListenerLoop);
                wakeupThread.IsBackground = true;
                wakeupThread.Name = "WakeupListener";
                wakeupThread.Start();
            }
        }

        protected override void SetVisibleCore(bool value) {
            base.SetVisibleCore(false);
        }

        private void WakeupListenerLoop() {
            while (!exiting) {
                try {
                    wakeupEvent.WaitOne();
                    if (exiting) break;
                    if (!IsDisposed) {
                        BeginInvoke(new Action(delegate() { OpenFlyout(false); }));
                    }
                } catch {
                    break;
                }
            }
        }

        private void OpenFlyout(bool expandSetup) {
            if (exiting) return;
            currentBrightness = display.RefreshCurrentValue();
            UpdateTrayVisuals(false);
            contextMenu.SyncBrightness(currentBrightness);
            flyout.ShowFlyout(Interop.GetNotifyIconBounds(trayIcon), currentBrightness, expandSetup);
        }

        private void OnTrayIconMouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Right && flyout.Visible) {
                flyout.Hide();
            }
        }

        private void OnTrayIconMouseUp(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                bool recentlyClosed = (DateTime.UtcNow - flyout.LastDeactivatedUtc).TotalMilliseconds < 250;
                if (flyout.Visible) {
                    flyout.Hide();
                } else if (!recentlyClosed) {
                    OpenFlyout(false);
                }
            }
        }

        private void OnUiBrightnessRequested(int level) {
            int clamped = Math.Max(0, Math.Min(100, level));
            currentBrightness = clamped;
            display.SetBrightnessAsync(clamped);

            if (flyout.IsUserDragging || contextMenu.IsUserDragging) {
                // Debounce Explorer tray icon IPC while dragging so the slider stays 60+ FPS
                if (!iconDebounceTimer.Enabled) {
                    iconDebounceTimer.Start();
                }
            } else {
                iconDebounceTimer.Stop();
                UpdateTrayVisuals(false);
            }

            if (!flyout.IsUserDragging) flyout.UpdateBrightnessExternal(clamped);
            if (!contextMenu.IsUserDragging) contextMenu.SyncBrightness(clamped);
        }

        private void FlushTrayVisuals() {
            iconDebounceTimer.Stop();
            UpdateTrayVisuals(false);
            if (!flyout.IsUserDragging) flyout.UpdateBrightnessExternal(currentBrightness);
            if (!contextMenu.IsUserDragging) contextMenu.SyncBrightness(currentBrightness);
        }

        private void OnExternalBrightnessChanged(int level) {
            if (exiting || IsDisposed) return;
            try {
                BeginInvoke(new Action(delegate() {
                    if (flyout.IsUserDragging || contextMenu.IsUserDragging) return;
                    currentBrightness = Math.Max(0, Math.Min(100, level));
                    UpdateTrayVisuals(false);
                    flyout.UpdateBrightnessExternal(currentBrightness);
                    contextMenu.SyncBrightness(currentBrightness);
                }));
            } catch {
            }
        }

        private void OnMouseWheel(Point mousePosition, int delta) {
            Rectangle bounds = Interop.GetNotifyIconBounds(trayIcon);
            if (bounds.IsEmpty || !bounds.Contains(mousePosition)) return;

            int sign = Math.Sign(delta);
            if (sign == 0) return;

            BeginInvoke(new Action(delegate() {
                int step = SettingsStore.WheelStep;
                int next = Math.Max(0, Math.Min(100, currentBrightness + sign * step));
                OnUiBrightnessRequested(next);
            }));
        }

        private void UpdateTrayVisuals(bool forceIcon) {
            string title = "Brightness Control - " + currentBrightness + "%";
            if (trayIcon.Text != title) {
                trayIcon.Text = title;
            }

            bool lightMode = SettingsStore.IsWindowsInLightMode();
            if (!forceIcon && hasRenderedIcon && lastIconBrightness == currentBrightness && lastIconLightMode == lightMode) {
                return;
            }

            Icon nextIcon = IconRenderer.CreateTrayIcon(currentBrightness, lightMode);
            if (nextIcon != null) {
                Icon old = trayIcon.Icon;
                trayIcon.Icon = nextIcon;
                if (old != null) old.Dispose();
                lastIconBrightness = currentBrightness;
                lastIconLightMode = lightMode;
                hasRenderedIcon = true;
            }
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e) {
            display.InvalidateMonitors();
            TriggerThemeDebounce();
        }

        private void OnSystemChanged(object sender, EventArgs e) {
            TriggerThemeDebounce();
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) {
            TriggerThemeDebounce();
        }

        private void TriggerThemeDebounce() {
            if (exiting || IsDisposed) return;
            try {
                BeginInvoke(new Action(delegate() {
                    themeDebounceTimer.Stop();
                    themeDebounceTimer.Start();
                }));
            } catch {
            }
        }

        private void ShowAbout() {
            Assembly asm = Assembly.GetExecutingAssembly();
            Version v = asm.GetName().Version;
            string msg = "Brightness Control";
            string note = "Version: " + v.ToString(2) + " • Author: Al Helal Mohammod Bayijid" + Environment.NewLine
                + "https://github.com/Dsx7/BrightnessControl";
            using (Bitmap bmp = IconRenderer.GetAppIconBitmap()) {
                BalloonTooltip.Show(msg, bmp, note, 0);
            }
        }

        private void ExitApp() {
            exiting = true;
            Application.Exit();
        }

        protected override void Dispose(bool disposing) {
            exiting = true;
            if (disposing) {
                SystemEvents.PaletteChanged -= OnSystemChanged;
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

                if (wakeupEvent != null) {
                    try { wakeupEvent.Set(); } catch { }
                }

                themeDebounceTimer.Dispose();
                iconDebounceTimer.Dispose();
                input.Dispose();
                display.Dispose();
                flyout.Dispose();
                contextMenu.Dispose();

                if (trayIcon != null) {
                    trayIcon.Visible = false;
                    if (trayIcon.Icon != null) {
                        trayIcon.Icon.Dispose();
                        trayIcon.Icon = null;
                    }
                    trayIcon.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
