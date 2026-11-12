using System;
using System.Threading;
using System.Windows.Forms;

namespace BrightnessControl {
    internal static class Program {
        private const string MutexName = "BrightnessControl_SingleInstance_Mutex";
        private const string WakeupEventName = "BrightnessControl_Wakeup_Event";

        [STAThread]
        private static void Main(string[] args) {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew)) {
                EventWaitHandle wakeup = null;
                try {
                    wakeup = new EventWaitHandle(false, EventResetMode.AutoReset, WakeupEventName);
                } catch {
                }

                if (!createdNew) {
                    bool isStartup = args != null && args.Length > 0 && string.Equals(args[0], "--startup", StringComparison.OrdinalIgnoreCase);
                    if (!isStartup && wakeup != null) {
                        try { wakeup.Set(); } catch { }
                    }
                    if (wakeup != null) wakeup.Dispose();
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (TrayApplication app = new TrayApplication(wakeup)) {
                    Application.Run(app);
                }

                if (wakeup != null) wakeup.Dispose();
            }
        }
    }
}

