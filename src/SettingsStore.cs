using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BrightnessControl {
    internal static class SettingsStore {
        private const string AppRegPath = @"Software\BrightnessControl";
        private const string RunRegPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupValueName = "Brightness Control";

        private static readonly int[] DefaultPresets = new int[] { 0, 25, 50, 75, 100 };

        public static int WheelStep {
            get {
                try {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppRegPath, false)) {
                        if (key != null) {
                            object v = key.GetValue("WheelStep");
                            if (v != null) {
                                int step = Convert.ToInt32(v);
                                if (step >= 1 && step <= 25) return step;
                            }
                        }
                    }
                } catch {
                }
                return 5;
            }
            set {
                int clamped = Math.Max(1, Math.Min(25, value));
                try {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(AppRegPath)) {
                        if (key != null) key.SetValue("WheelStep", clamped, RegistryValueKind.DWord);
                    }
                } catch {
                }
            }
        }

        public static bool ShowPresets {
            get {
                try {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppRegPath, false)) {
                        if (key != null) {
                            object v = key.GetValue("ShowPresets");
                            if (v != null) return Convert.ToInt32(v) != 0;
                        }
                    }
                } catch {
                }
                return true;
            }
            set {
                try {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(AppRegPath)) {
                        if (key != null) key.SetValue("ShowPresets", value ? 1 : 0, RegistryValueKind.DWord);
                    }
                } catch {
                }
            }
        }

        public static int[] Presets {
            get {
                try {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppRegPath, false)) {
                        if (key != null) {
                            string raw = key.GetValue("Presets") as string;
                            if (!string.IsNullOrEmpty(raw)) {
                                int[] parsed = ParsePresets(raw);
                                if (parsed.Length > 0) return parsed;
                            }
                        }
                    }
                } catch {
                }
                return (int[])DefaultPresets.Clone();
            }
            set {
                int[] toSave = (value != null && value.Length > 0) ? value : DefaultPresets;
                string[] parts = new string[toSave.Length];
                for (int i = 0; i < toSave.Length; i++) parts[i] = toSave[i].ToString();
                try {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(AppRegPath)) {
                        if (key != null) key.SetValue("Presets", string.Join(",", parts), RegistryValueKind.String);
                    }
                } catch {
                }
            }
        }

        public static void ResetPresets() {
            Presets = (int[])DefaultPresets.Clone();
        }

        public static string FormatPresets(int[] presets) {
            if (presets == null || presets.Length == 0) presets = DefaultPresets;
            string[] parts = new string[presets.Length];
            for (int i = 0; i < presets.Length; i++) parts[i] = presets[i].ToString();
            return string.Join(", ", parts);
        }

        public static int[] ParsePresets(string raw) {
            if (string.IsNullOrEmpty(raw)) return new int[0];
            string[] tokens = raw.Split(new char[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            SortedDictionary<int, bool> unique = new SortedDictionary<int, bool>();
            foreach (string t in tokens) {
                string clean = t.Trim().TrimEnd('%');
                int val;
                if (int.TryParse(clean, out val)) {
                    if (val < 0) val = 0;
                    if (val > 100) val = 100;
                    unique[val] = true;
                }
            }
            List<int> list = new List<int>(unique.Keys);
            if (list.Count > 8) list.RemoveRange(8, list.Count - 8);
            return list.ToArray();
        }

        public static bool IsWindowsInLightMode() {
            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", false)) {
                    if (key != null) {
                        object v = key.GetValue("SystemUsesLightTheme");
                        if (v != null) return Convert.ToInt32(v) == 1;
                    }
                }
            } catch {
            }
            return false;
        }

        public static bool StartsWithWindows() {
            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegPath, false)) {
                    if (key == null) return false;
                    string val = key.GetValue(StartupValueName) as string;
                    if (string.IsNullOrEmpty(val)) return false;
                    string exe = Application.ExecutablePath;
                    return string.Equals(val.Trim('"'), exe, StringComparison.OrdinalIgnoreCase);
                }
            } catch {
                return false;
            }
        }

        public static bool SetStartup(bool enable) {
            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegPath, true)) {
                    if (key == null) return false;
                    if (enable) {
                        key.SetValue(StartupValueName, "\"" + Application.ExecutablePath + "\"");
                    } else {
                        key.DeleteValue(StartupValueName, false);
                    }
                    return enable;
                }
            } catch {
                return !enable;
            }
        }
    }
}

