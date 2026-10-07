using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

namespace EdgeMotion
{
    internal static class Native
    {
        internal delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct MouseData { public Point Point; public uint MouseInfo, Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct KeyboardData { public uint Key, Scan, Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
        [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWindowsHookEx(int hook, HookProc callback, IntPtr module, uint thread);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string module);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr window, StringBuilder text, int length);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
        private static readonly object PhysicalSync = new object();
        private static readonly HashSet<ushort> PhysicalKeys = new HashSet<ushort>();
        private static readonly object InputSync = new object();
        private static readonly ShortcutSender Sender = new ShortcutSender(SendKeys, PhysicallyHeld);
        private static readonly System.Threading.Timer Watchdog = new System.Threading.Timer(delegate { Sender.ReleaseOwned(); }, null, 100, 100);
        internal static void TrackPhysicalKey(ushort key, bool down)
        { lock (PhysicalSync) { if (down) PhysicalKeys.Add(key); else PhysicalKeys.Remove(key); } }
        private static bool PhysicallyHeld(ushort key) { lock (PhysicalSync) return PhysicalKeys.Contains(key); }
        internal static void InitializePhysicalKeys()
        { for (ushort key = 1; key < 256; key++) if ((GetAsyncKeyState(key) & 0x8000) != 0) TrackPhysicalKey(key, true); }
        private static uint SendKeys(KeyStroke[] strokes)
        {
            var inputs = new Input[strokes.Length];
            for (int i = 0; i < strokes.Length; i++)
            {
                ushort key = strokes[i].Key;
                bool extended = key == 0x5B || key == 0x5C || key == 0xA3 || key == 0xA5 || (key >= 0x21 && key <= 0x2E);
                inputs[i] = new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Key = key, Flags = (strokes[i].Up ? 2u : 0u) | (extended ? 1u : 0u), Extra = new UIntPtr(0x454D) } } };
            }
            return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input)));
        }
        internal static void ReleaseOwnedKeys() { Sender.ReleaseOwned(); }
        internal static void RecoverModifiers()
        {
            lock (InputSync)
            {
                Sender.ReleaseOwned();
                var releases = new System.Collections.Generic.List<KeyStroke>();
                foreach (ushort key in new ushort[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C })
                { TrackPhysicalKey(key, false); releases.Add(new KeyStroke(key, true)); }
                if (SendKeys(releases.ToArray()) != releases.Count)
                    throw new InvalidOperationException("Windows не приняла восстановление ввода.");
            }
        }

        internal static void RightClick()
        {
            lock (InputSync)
            {
                var inputs = new[] { new Input { Type = 0, Data = new InputUnion { Mouse = new MouseInput { Flags = 0x8 } } },
                         new Input { Type = 0, Data = new InputUnion { Mouse = new MouseInput { Flags = 0x10 } } } };
                uint count = SendInput(2, inputs, Marshal.SizeOf(typeof(Input)));
                if (count == 1) SendInput(1, new[] { inputs[1] }, Marshal.SizeOf(typeof(Input)));
                if (count != 2) throw new InvalidOperationException("Windows не приняла правый клик.");
            }
        }

        internal static void Execute(DesktopAction action)
        {
            ushort[] keys;
            switch (action)
            {
                case DesktopAction.Desktop: keys = new ushort[] { 0x5B, 0x44 }; break;
                case DesktopAction.TaskView: keys = new ushort[] { 0x5B, 0x09 }; break;
                case DesktopAction.Back: keys = new ushort[] { 0xA4, 0x25 }; break;
                case DesktopAction.VoiceTyping: keys = new ushort[] { 0x5B, 0x48 }; break;
                case DesktopAction.QuickSettings: keys = new ushort[] { 0x5B, 0x41 }; break;
                case DesktopAction.PreviousApp: keys = new ushort[] { 0xA4, 0x09 }; break;
                case DesktopAction.NextApp: keys = new ushort[] { 0xA4, 0xA0, 0x09 }; break;
                case DesktopAction.Start: keys = new ushort[] { 0x5B }; break;
                case DesktopAction.Search: keys = new ushort[] { 0x5B, 0x53 }; break;
                default: return;
            }
            // Avoid combining a generated chord with modifiers physically held by the user.
            foreach (int modifier in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C })
                if ((GetAsyncKeyState(modifier) & 0x8000) != 0)
                    throw new InvalidOperationException("Отпустите Shift, Ctrl, Alt и Win перед жестом.");
            lock (InputSync) Sender.Execute(keys);
            GC.KeepAlive(Watchdog);
        }
    }
}
