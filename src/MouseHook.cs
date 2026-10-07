using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EdgeMotion
{
    internal sealed class MouseHook : IDisposable
    {
        private readonly Native.HookProc mouseCallback, keyboardCallback;
        private readonly Control dispatcher;
        private readonly Settings settings;
        private readonly GestureRecognizer recognizer = new GestureRecognizer();
        private readonly GestureDispatchGate gate = new GestureDispatchGate();
        private readonly GestureDispatchGate clickGate = new GestureDispatchGate(0);
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Thread thread;
        private readonly ManualResetEvent ready = new ManualResetEvent(false);
        private IntPtr mouseHandle, keyboardHandle;
        private Control hookDispatcher;
        private Exception startupError;
        private bool captured, cancelUntilRelease;
        private volatile bool enabled = true, disposed;
        private Point current;
        private IntPtr targetWindow;
        public event Action<Gesture> Recognized;
        public event Action<string> Error;
        public bool Enabled { get { return enabled; } set { enabled = value; if (!value) { gate.Cancel(); clickGate.Cancel(); } } }

        public MouseHook(Control dispatcher, Settings settings)
        {
            this.dispatcher = dispatcher; this.settings = settings;
            mouseCallback = Process; keyboardCallback = Keyboard;
            thread = new Thread(Run) { IsBackground = true, Name = "EdgeMotion input hooks" };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            if (!ready.WaitOne(3000)) { Dispose(); throw new TimeoutException("Поток ввода не ответил."); }
            if (startupError != null) { Dispose(); throw startupError; }
        }
        private void Run()
        {
            try
            {
                using (var control = new Control())
                using (var timer = new System.Windows.Forms.Timer { Interval = 40 })
                {
                    hookDispatcher = control; IntPtr unused = control.Handle;
                    Native.InitializePhysicalKeys();
                    keyboardHandle = Native.SetWindowsHookEx(13, keyboardCallback, Native.GetModuleHandle(null), 0);
                    if (keyboardHandle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                    mouseHandle = Native.SetWindowsHookEx(14, mouseCallback, Native.GetModuleHandle(null), 0);
                    if (mouseHandle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                    timer.Tick += delegate
                    {
                        if (!captured) return;
                        if (!enabled || (Native.GetAsyncKeyState(0x1B) & 0x8000) != 0)
                        { recognizer.Cancel(); cancelUntilRelease = true; }
                        recognizer.EvaluateHold(current, clock.ElapsedMilliseconds);
                    };
                    timer.Start(); ready.Set();
                    if (!disposed) Application.Run();
                }
            }
            catch (Exception ex) { startupError = ex; ready.Set(); PostError(ex.Message); }
            finally
            {
                if (mouseHandle != IntPtr.Zero) Native.UnhookWindowsHookEx(mouseHandle);
                if (keyboardHandle != IntPtr.Zero) Native.UnhookWindowsHookEx(keyboardHandle);
            }
        }
        private IntPtr Keyboard(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0)
            {
                try
                {
                    var info = (Native.KeyboardData)Marshal.PtrToStructure(data, typeof(Native.KeyboardData));
                    if ((info.Flags & 0x10) == 0)
                    {
                        int kind = message.ToInt32();
                        ushort key = (ushort)info.Key;
                        if (key == 0x10) key = info.Scan == 0x36 ? (ushort)0xA1 : (ushort)0xA0;
                        else if (key == 0x11) key = (info.Flags & 1) != 0 ? (ushort)0xA3 : (ushort)0xA2;
                        else if (key == 0x12) key = (info.Flags & 1) != 0 ? (ushort)0xA5 : (ushort)0xA4;
                        Native.TrackPhysicalKey(key, kind == 0x100 || kind == 0x104);
                        if (info.Key == 0x1B && (kind == 0x100 || kind == 0x104))
                        { recognizer.Cancel(); cancelUntilRelease = captured; gate.Cancel(); }
                    }
                }
                catch { }
            }
            return Native.CallNextHookEx(keyboardHandle, code, message, data);
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
            try { if (!disposed && !dispatcher.IsDisposed && dispatcher.IsHandleCreated) dispatcher.BeginInvoke(action); }
            catch (InvalidOperationException) { }
        }
        private void PostError(string message) { Post(delegate { if (Error != null) Error(message); }); }
        private void Dispatch(Gesture gesture, bool click, long now, IntPtr target)
        {
            int ticket;
            GestureDispatchGate selectedGate = click ? clickGate : gate;
            if (!selectedGate.TryEnter(now, out ticket)) return;
            DesktopAction action = settings.ActionFor(gesture);
            bool testMode = settings.TestMode;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (!enabled || disposed || !selectedGate.IsCurrent(ticket, now, clock.ElapsedMilliseconds) || Native.GetForegroundWindow() != target) return;
                    if (click)
                    {
                        if ((Native.GetAsyncKeyState(0x02) & 0x8000) == 0) Native.RightClick();
                    }
                    else
                    {
                        if (!testMode) Native.Execute(action);
                        Post(delegate { if (enabled && Recognized != null) Recognized(gesture); });
                    }
                }
                catch (Exception ex) { enabled = false; gate.Cancel(); PostError("Жесты остановлены. " + ex.Message); }
                finally { Native.ReleaseOwnedKeys(); selectedGate.Leave(); }
            });
        }
        private IntPtr Process(int code, IntPtr message, IntPtr data)
        {
            if (code < 0 || disposed) return Native.CallNextHookEx(mouseHandle, code, message, data);
            int kind = message.ToInt32();
            try
            {
                var info = (Native.MouseData)Marshal.PtrToStructure(data, typeof(Native.MouseData));
                if ((info.Flags & 1) != 0) return Native.CallNextHookEx(mouseHandle, code, message, data);
                current = new Point(info.Point.X, info.Point.Y);
                long now = clock.ElapsedMilliseconds;
                if (kind == 0x204 && enabled && !captured)
                {
                    Rectangle bounds = Screen.FromPoint(current).Bounds;
                    recognizer.EdgeWidth = settings.EdgeWidth; recognizer.Distance = settings.Distance; recognizer.HoldMilliseconds = settings.HoldMilliseconds;
                    if ((!settings.SkipFullscreen || !Fullscreen(bounds)) && recognizer.Begin(current, bounds, now))
                    { captured = true; cancelUntilRelease = false; targetWindow = Native.GetForegroundWindow(); return new IntPtr(1); }
                }
                if (captured && (!enabled || (Native.GetAsyncKeyState(0x1B) & 0x8000) != 0))
                { recognizer.Cancel(); cancelUntilRelease = true; }
                if (captured && kind == 0x200) recognizer.Move(current, now);
                if (captured && kind == 0x205)
                {
                    Gesture result = cancelUntilRelease ? Gesture.None : recognizer.End(current, now);
                    bool click = recognizer.IsClick && !cancelUntilRelease;
                    captured = false; cancelUntilRelease = false;
                    if (result != Gesture.None || click) Dispatch(result, click, now, targetWindow);
                    return new IntPtr(1);
                }
            }
            catch (Exception ex)
            {
                bool swallowUp = captured && kind == 0x205;
                recognizer.Cancel(); cancelUntilRelease = captured; enabled = false; gate.Cancel();
                if (swallowUp) captured = false;
                PostError("Жесты остановлены. " + ex.Message);
                if (swallowUp) return new IntPtr(1);
            }
            return Native.CallNextHookEx(mouseHandle, code, message, data);
        }
        public void Dispose()
        {
            disposed = true; enabled = false; gate.Cancel(); clickGate.Cancel();
            try { if (hookDispatcher != null && hookDispatcher.IsHandleCreated) hookDispatcher.BeginInvoke(new Action(Application.ExitThread)); }
            catch (InvalidOperationException) { }
            if (thread != Thread.CurrentThread) thread.Join(1000);
            GC.KeepAlive(mouseCallback); GC.KeepAlive(keyboardCallback);
        }
    }
}
