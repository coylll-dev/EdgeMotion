using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace EdgeMotion
{
    internal static class Native
    {
        internal delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct MouseData { public Point Point; public uint MouseInfo, Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
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

        private static void Send(Input[] inputs)
        {
            if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input))) != inputs.Length)
            {
                // Release every synthetic key even if a chord was only partially inserted.
                foreach (var input in inputs)
                    if (input.Type == 1 && input.Data.Keyboard.Flags == 0)
                    {
                        var release = input;
                        release.Data.Keyboard.Flags = 2;
                        SendInput(1, new[] { release }, Marshal.SizeOf(typeof(Input)));
                    }
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows не приняла ввод. Проверьте права целевого приложения.");
            }
        }

        internal static void RightClick()
        {
            Send(new[] { new Input { Type = 0, Data = new InputUnion { Mouse = new MouseInput { Flags = 0x8 } } },
                         new Input { Type = 0, Data = new InputUnion { Mouse = new MouseInput { Flags = 0x10 } } } });
        }

        internal static void Execute(DesktopAction action)
        {
            ushort[] keys;
            switch (action)
            {
                case DesktopAction.Desktop: keys = new ushort[] { 0x5B, 0x44 }; break;
                case DesktopAction.TaskView: keys = new ushort[] { 0x5B, 0x09 }; break;
                case DesktopAction.Back: keys = new ushort[] { 0x12, 0x25 }; break;
                case DesktopAction.VoiceTyping: keys = new ushort[] { 0x5B, 0x48 }; break;
                case DesktopAction.QuickSettings: keys = new ushort[] { 0x5B, 0x41 }; break;
                case DesktopAction.PreviousApp: keys = new ushort[] { 0x12, 0x09 }; break;
                case DesktopAction.NextApp: keys = new ushort[] { 0x12, 0x10, 0x09 }; break;
                case DesktopAction.Start: keys = new ushort[] { 0x5B }; break;
                case DesktopAction.Search: keys = new ushort[] { 0x5B, 0x53 }; break;
                default: return;
            }
            // Avoid combining a generated chord with modifiers physically held by the user.
            foreach (int modifier in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C })
                if ((GetAsyncKeyState(modifier) & 0x8000) != 0)
                    throw new InvalidOperationException("Отпустите Shift, Ctrl, Alt и Win перед жестом.");
            var inputs = new Input[keys.Length * 2];
            for (int i = 0; i < keys.Length; i++)
            {
                inputs[i] = new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Key = keys[i] } } };
                inputs[keys.Length + i] = new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Key = keys[keys.Length - i - 1], Flags = 2 } } };
            }
            Send(inputs);
        }
    }
}
