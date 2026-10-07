using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace EdgeMotion
{
    internal sealed class MouseHook : IDisposable
    {
        private readonly Native.HookProc callback;
        private readonly Control dispatcher;
        private readonly Settings settings;
        private readonly GestureRecognizer recognizer = new GestureRecognizer();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Timer timer = new Timer { Interval = 30 };
        private IntPtr handle;
        private bool captured, cancelUntilRelease;
        private bool enabled = true;
        private Point current;
        public event Action<Gesture> Recognized;
        public event Action<string> Error;
        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; if (!value) { recognizer.Cancel(); cancelUntilRelease = captured; } }
        }

        public MouseHook(Control dispatcher, Settings settings)
        {
            this.dispatcher = dispatcher; this.settings = settings;
            callback = Process;
            handle = Native.SetWindowsHookEx(14, callback, Native.GetModuleHandle(null), 0);
            if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            timer.Tick += delegate
            {
                if (!captured) return;
                if ((Native.GetAsyncKeyState(0x1B) & 0x8000) != 0) { recognizer.Cancel(); cancelUntilRelease = true; }
                recognizer.EvaluateHold(current, clock.ElapsedMilliseconds);
            };
            timer.Start();
        }

        private bool Fullscreen(Rectangle bounds)
        {
            IntPtr window = Native.GetForegroundWindow();
            var name = new StringBuilder(256); Native.GetClassName(window, name, name.Capacity);
            if (name.ToString() == "Progman" || name.ToString() == "WorkerW") return false;
            Native.Rect rect;
            return Native.GetWindowRect(window, out rect) && rect.Left <= bounds.Left && rect.Top <= bounds.Top && rect.Right >= bounds.Right && rect.Bottom >= bounds.Bottom;
        }

        private void Post(Action action)
        {
            if (!dispatcher.IsDisposed && dispatcher.IsHandleCreated) dispatcher.BeginInvoke(action);
        }

        private IntPtr Process(int code, IntPtr message, IntPtr data)
        {
            if (code < 0) return Native.CallNextHookEx(handle, code, message, data);
            try
            {
                var info = (Native.MouseData)Marshal.PtrToStructure(data, typeof(Native.MouseData));
                if ((info.Flags & 1) != 0) return Native.CallNextHookEx(handle, code, message, data);
                current = new Point(info.Point.X, info.Point.Y);
                int kind = message.ToInt32(); long now = clock.ElapsedMilliseconds;
                if (kind == 0x204 && Enabled && !captured)
                {
                    Rectangle bounds = Screen.FromPoint(current).Bounds;
                    recognizer.EdgeWidth = settings.EdgeWidth; recognizer.Distance = settings.Distance; recognizer.HoldMilliseconds = settings.HoldMilliseconds;
                    if ((!settings.SkipFullscreen || !Fullscreen(bounds)) && recognizer.Begin(current, bounds, now))
                    { captured = true; cancelUntilRelease = false; return new IntPtr(1); }
                }
                if (captured && kind == 0x200) recognizer.Move(current, now);
                if (captured && kind == 0x205)
                {
                    Gesture result = cancelUntilRelease ? Gesture.None : recognizer.End(current, now);
                    bool click = recognizer.IsClick && !cancelUntilRelease;
                    captured = false; cancelUntilRelease = false;
                    if (result != Gesture.None) Post(delegate { if (Recognized != null) Recognized(result); });
                    else if (click) Post(delegate { try { Native.RightClick(); } catch (Exception ex) { if (Error != null) Error(ex.Message); } });
                    return new IntPtr(1);
                }
            }
            catch (Exception ex)
            {
                recognizer.Cancel(); cancelUntilRelease = captured;
                Post(delegate { if (Error != null) Error(ex.Message); });
            }
            return Native.CallNextHookEx(handle, code, message, data);
        }

        public void Dispose()
        {
            timer.Stop(); timer.Dispose();
            if (handle != IntPtr.Zero) { Native.UnhookWindowsHookEx(handle); handle = IntPtr.Zero; }
            GC.KeepAlive(callback);
        }
    }
}
