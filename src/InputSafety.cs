using System;
using System.Collections.Generic;

namespace EdgeMotion
{
    public struct KeyStroke
    {
        public ushort Key;
        public bool Up;
        public KeyStroke(ushort key, bool up) { Key = key; Up = up; }
    }

    // Owns only keys successfully inserted by this sender. No blanket resets on failure.
    public sealed class ShortcutSender
    {
        private readonly Func<KeyStroke[], uint> send;
        private readonly Func<ushort, bool> physicallyHeld;
        private readonly HashSet<ushort> owned = new HashSet<ushort>();
        private readonly object sync = new object();
        public ShortcutSender(Func<KeyStroke[], uint> send, Func<ushort, bool> physicallyHeld)
        { this.send = send; this.physicallyHeld = physicallyHeld; }
        public int PendingKeys { get { lock (sync) return owned.Count; } }

        public void Execute(ushort[] keys)
        {
            lock (sync)
            {
                ReleaseOwned();
                if (owned.Count != 0) throw new InvalidOperationException("Ввод приостановлен: не удалось отпустить клавиши предыдущего действия.");
                foreach (ushort key in keys)
                    if (physicallyHeld(key)) throw new InvalidOperationException("Отпустите клавиши перед жестом.");
                var packet = new KeyStroke[keys.Length * 2];
                for (int i = 0; i < keys.Length; i++)
                { packet[i] = new KeyStroke(keys[i], false); packet[keys.Length + i] = new KeyStroke(keys[keys.Length - 1 - i], true); }
                try
                {
                    uint inserted = send(packet);
                    for (int i = 0; i < Math.Min(inserted, packet.Length); i++)
                    { if (packet[i].Up) owned.Remove(packet[i].Key); else owned.Add(packet[i].Key); }
                    if (inserted != packet.Length) throw new InvalidOperationException("Windows приняла только часть сочетания. Жесты остановлены; проверьте права активной программы.");
                }
                finally { ReleaseOwned(); }
            }
        }

        public void ReleaseOwned()
        {
            lock (sync)
            {
                foreach (ushort key in new List<ushort>(owned))
                {
                    // A real press that occurred during injection belongs to the user.
                    if (physicallyHeld(key)) continue;
                    try { if (send(new[] { new KeyStroke(key, true) }) == 1) owned.Remove(key); }
                    catch { /* Keep ownership for the watchdog's next attempt. */ }
                }
            }
        }
    }

    public sealed class GestureDispatchGate
    {
        private readonly object sync = new object();
        private long last = -1000;
        private bool busy;
        private int generation;
        private readonly int cooldown;
        public GestureDispatchGate(int cooldown = 300) { this.cooldown = cooldown; }
        public bool TryEnter(long now, out int ticket)
        {
            lock (sync)
            {
                ticket = generation;
                if (busy || now - last < cooldown) return false;
                busy = true; last = now; return true;
            }
        }
        public bool IsCurrent(int ticket, long created, long now)
        { lock (sync) return busy && ticket == generation && now - created <= 250; }
        public void Leave() { lock (sync) busy = false; }
        public void Cancel() { lock (sync) generation++; }
    }
}
