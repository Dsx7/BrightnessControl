using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BrightnessControl {
    internal sealed class InputListener : IDisposable {
        private IntPtr mouseHookId = IntPtr.Zero;
        private Interop.LowLevelMouseProc mouseCallback;

        public event Action<Point, int> MouseWheel;

        public void Listen() {
            Stop();
            IntPtr hInstance = Marshal.GetHINSTANCE(Assembly.GetExecutingAssembly().GetModules()[0]);
            mouseCallback = MouseHookCallback;
            mouseHookId = Interop.SetWindowsHookEx(Interop.WH_MOUSE_LL, mouseCallback, hInstance, 0);
        }

        public void Stop() {
            if (mouseHookId != IntPtr.Zero) {
                Interop.UnhookWindowsHookEx(mouseHookId);
                mouseHookId = IntPtr.Zero;
            }
            mouseCallback = null;
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0 && wParam == (IntPtr)Interop.WM_MOUSEWHEEL) {
                try {
                    Interop.MSLLHOOKSTRUCT hook = (Interop.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Interop.MSLLHOOKSTRUCT));
                    Action<Point, int> handler = MouseWheel;
                    if (handler != null) {
                        handler(new Point(hook.pt.X, hook.pt.Y), hook.WheelDelta);
                    }
                } catch {
                }
            }
            return Interop.CallNextHookEx(mouseHookId, nCode, wParam, lParam);
        }

        public void Dispose() {
            Stop();
        }
    }
}

