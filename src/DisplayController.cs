using System;
using System.Collections.Generic;
using System.Management;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace BrightnessControl {
    internal sealed class DisplayController : IDisposable {
        private readonly object wmiLock = new object();
        private readonly object queueLock = new object();
        private readonly object monitorLock = new object();
        private readonly AutoResetEvent workSignal = new AutoResetEvent(false);
        private readonly Thread workerThread;

        private ManagementEventWatcher wmiWatcher;
        private List<Interop.PHYSICAL_MONITOR> cachedMonitors;
        private Interop.PHYSICAL_MONITOR cachedPrimaryMonitor;
        private bool monitorsLoaded;

        private bool wmiProbed;
        private bool wmiSupported;
        private List<ManagementObject> cachedWmiWriters;

        private volatile bool disposed;
        private int currentValue;
        private int pendingValue = -1;
        private int lastAppliedValue = -1;
        private DateTime lastWriteUtc = DateTime.MinValue;

        public event Action<int> BrightnessChanged;

        public DisplayController() {
            currentValue = SettingsStore.LastBrightness;
            int hardwareVal = ReadCurrentBrightness();
            if (hardwareVal >= 0) {
                currentValue = hardwareVal;
            }
            StartWmiWatcher();
            workerThread = new Thread(WorkerLoop);
            workerThread.IsBackground = true;
            workerThread.Name = "BrightnessWorker";
            workerThread.Start();
        }

        public int CurrentValue {
            get { return currentValue; }
        }

        public int RefreshCurrentValue() {
            int val = ReadCurrentBrightness();
            if (val >= 0) currentValue = val;
            return currentValue;
        }

        public void InvalidateMonitors() {
            lock (monitorLock) {
                ReleaseCachedMonitors();
                monitorsLoaded = false;
            }
        }

        public void SetBrightnessAsync(int level) {
            int clamped = Math.Max(0, Math.Min(100, level));
            currentValue = clamped;
            SettingsStore.LastBrightness = clamped;
            lock (queueLock) {
                pendingValue = clamped;
            }
            workSignal.Set();
        }

        public void TurnOffScreen() {
            Thread t = new Thread(delegate() {
                try {
                    Thread.Sleep(500);
                    Interop.SendMessage(Interop.HWND_BROADCAST, Interop.WM_SYSCOMMAND, Interop.SC_MONITORPOWER, 2);
                } catch {
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void WorkerLoop() {
            while (!disposed) {
                workSignal.WaitOne();
                while (!disposed) {
                    int next = -1;
                    lock (queueLock) {
                        if (pendingValue < 0) break;
                        next = pendingValue;
                        pendingValue = -1;
                    }
                    ApplyBrightnessHardware(next);
                }
            }
        }

        private void EnsureMonitorsLoaded() {
            lock (monitorLock) {
                if (monitorsLoaded && cachedMonitors != null && cachedMonitors.Count > 0) return;
                cachedMonitors = CollectPhysicalMonitors(out cachedPrimaryMonitor);
                monitorsLoaded = (cachedMonitors != null && cachedMonitors.Count > 0);
            }
        }

        private void ReleaseCachedMonitors() {
            if (cachedMonitors != null && cachedMonitors.Count > 0) {
                DestroyPhysicalMonitors(cachedMonitors);
            }
            cachedMonitors = null;
            cachedPrimaryMonitor = new Interop.PHYSICAL_MONITOR();
        }

        private void ApplyBrightnessHardware(int level) {
            lastWriteUtc = DateTime.UtcNow;
            lastAppliedValue = level;

            // 1. External monitors via DDC/CI (Dxva2) using cached handles (zero 225ms re-enumeration per tick!)
            try {
                EnsureMonitorsLoaded();
                bool anyFailed = false;
                lock (monitorLock) {
                    if (cachedMonitors != null && cachedMonitors.Count > 0) {
                        foreach (Interop.PHYSICAL_MONITOR pm in cachedMonitors) {
                            try {
                                if (!Interop.SetMonitorBrightness(pm.hPhysicalMonitor, (uint)level)) {
                                    anyFailed = true;
                                }
                            } catch {
                                anyFailed = true;
                            }
                        }
                        if (anyFailed) {
                            ReleaseCachedMonitors();
                            monitorsLoaded = false;
                        }
                    }
                }
            } catch {
            }

            // 2. Laptop / integrated display via WMI (skip completely if probed unsupported)
            ApplyWmiBrightness(level);
        }

        private void ApplyWmiBrightness(int level) {
            try {
                lock (wmiLock) {
                    if (wmiProbed && !wmiSupported) return;

                    if (!wmiProbed) {
                        wmiProbed = true;
                        try {
                            using (ManagementClass mclass = new ManagementClass("WmiMonitorBrightnessMethods")) {
                                mclass.Scope = new ManagementScope(@"\\.\root\wmi");
                                ManagementObjectCollection instances = mclass.GetInstances();
                                cachedWmiWriters = new List<ManagementObject>();
                                foreach (ManagementObject inst in instances) {
                                    cachedWmiWriters.Add(inst);
                                }
                                wmiSupported = (cachedWmiWriters.Count > 0);
                            }
                        } catch {
                            wmiSupported = false;
                            ReleaseCachedWmiWriters();
                            return;
                        }
                    }

                    if (!wmiSupported || cachedWmiWriters == null || cachedWmiWriters.Count == 0) {
                        return;
                    }

                    foreach (ManagementObject instance in cachedWmiWriters) {
                        instance.InvokeMethod("WmiSetBrightness", new object[] { 0, level });
                    }
                }
            } catch {
                // Re-probe next time if WMI handle became stale (e.g., sleep/wake)
                lock (wmiLock) {
                    ReleaseCachedWmiWriters();
                    wmiProbed = false;
                }
            }
        }

        private void ReleaseCachedWmiWriters() {
            if (cachedWmiWriters != null) {
                foreach (ManagementObject obj in cachedWmiWriters) {
                    try { obj.Dispose(); } catch { }
                }
                cachedWmiWriters = null;
            }
        }

        private int ReadCurrentBrightness() {
            // Try DDC/CI primary monitor first using cached handle
            try {
                EnsureMonitorsLoaded();
                lock (monitorLock) {
                    IntPtr hMon = cachedPrimaryMonitor.hPhysicalMonitor;
                    if (hMon == IntPtr.Zero && cachedMonitors != null && cachedMonitors.Count > 0) {
                        hMon = cachedMonitors[0].hPhysicalMonitor;
                    }
                    if (hMon != IntPtr.Zero) {
                        uint min, cur, max;
                        if (Interop.GetMonitorBrightness(hMon, out min, out cur, out max)) {
                            int pct;
                            if (max > min) {
                                pct = (int)Math.Round((cur - min) * 100.0 / (max - min));
                            } else {
                                pct = (int)cur;
                            }
                            pct = Math.Max(0, Math.Min(100, pct));
                            SettingsStore.LastBrightness = pct;
                            return pct;
                        }
                    }
                }
            } catch {
            }

            // Fallback to WMI (skip if already probed unsupported)
            try {
                lock (wmiLock) {
                    if (wmiProbed && !wmiSupported) return SettingsStore.LastBrightness;
                    ManagementScope scope = new ManagementScope(@"root\WMI");
                    SelectQuery query = new SelectQuery("SELECT * FROM WmiMonitorBrightness");
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query))
                    using (ManagementObjectCollection results = searcher.Get()) {
                        foreach (ManagementBaseObject obj in results) {
                            using (obj) {
                                PropertyData prop = obj.Properties["CurrentBrightness"];
                                if (prop != null && prop.Value != null) {
                                    int b = Convert.ToInt32(prop.Value);
                                    b = Math.Max(0, Math.Min(100, b));
                                    SettingsStore.LastBrightness = b;
                                    return b;
                                }
                            }
                        }
                    }
                }
            } catch {
            }

            return SettingsStore.LastBrightness;
        }

        private void StartWmiWatcher() {
            try {
                ManagementScope scope = new ManagementScope(@"\\.\root\wmi");
                scope.Connect();
                EventQuery query = new EventQuery("SELECT * FROM WmiMonitorBrightnessEvent");
                wmiWatcher = new ManagementEventWatcher(scope, query);
                wmiWatcher.EventArrived += OnWmiEventArrived;
                wmiWatcher.Start();
            } catch {
                wmiWatcher = null;
            }
        }

        private void OnWmiEventArrived(object sender, EventArrivedEventArgs e) {
            try {
                if (disposed || e == null || e.NewEvent == null) return;
                object raw = e.NewEvent.Properties["Brightness"].Value;
                if (raw == null) return;
                int level = Math.Max(0, Math.Min(100, Convert.ToInt32(raw)));

                lock (queueLock) {
                    if (pendingValue >= 0) return;
                }
                if ((DateTime.UtcNow - lastWriteUtc).TotalMilliseconds < 400 && level != lastAppliedValue) {
                    return;
                }

                currentValue = level;
                SettingsStore.LastBrightness = level;
                Action<int> handler = BrightnessChanged;
                if (handler != null) handler(level);
            } catch {
            }
        }

        private static List<Interop.PHYSICAL_MONITOR> CollectPhysicalMonitors(out Interop.PHYSICAL_MONITOR primaryMonitor) {
            primaryMonitor = new Interop.PHYSICAL_MONITOR();
            List<Interop.PHYSICAL_MONITOR> list = new List<Interop.PHYSICAL_MONITOR>();
            FieldInfo hmonField = typeof(Screen).GetField("hmonitor", BindingFlags.Instance | BindingFlags.NonPublic);
            if (hmonField == null) return list;

            foreach (Screen screen in Screen.AllScreens) {
                IntPtr hmon = (IntPtr)hmonField.GetValue(screen);
                uint count;
                if (Interop.GetNumberOfPhysicalMonitorsFromHMONITOR(hmon, out count) && count > 0) {
                    Interop.PHYSICAL_MONITOR[] arr = new Interop.PHYSICAL_MONITOR[count];
                    if (Interop.GetPhysicalMonitorsFromHMONITOR(hmon, count, arr)) {
                        list.AddRange(arr);
                        if (screen.Primary && arr.Length > 0) {
                            primaryMonitor = arr[0];
                        }
                    }
                }
            }
            if (primaryMonitor.hPhysicalMonitor == IntPtr.Zero && list.Count > 0) {
                primaryMonitor = list[0];
            }
            return list;
        }

        private static void DestroyPhysicalMonitors(List<Interop.PHYSICAL_MONITOR> monitors) {
            if (monitors == null || monitors.Count == 0) return;
            try {
                Interop.PHYSICAL_MONITOR[] arr = monitors.ToArray();
                Interop.DestroyPhysicalMonitors((uint)arr.Length, arr);
            } catch {
            }
        }

        public void Dispose() {
            if (disposed) return;
            disposed = true;
            try { workSignal.Set(); } catch { }
            if (wmiWatcher != null) {
                try {
                    wmiWatcher.EventArrived -= OnWmiEventArrived;
                    wmiWatcher.Stop();
                    wmiWatcher.Dispose();
                } catch { }
                wmiWatcher = null;
            }
            lock (monitorLock) {
                ReleaseCachedMonitors();
            }
            lock (wmiLock) {
                ReleaseCachedWmiWriters();
            }
            try { workSignal.Dispose(); } catch { }
        }
    }
}
