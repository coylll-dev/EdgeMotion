using System;
using System.Drawing;

namespace EdgeMotion
{
    public enum Gesture { None, Home, Recent, Back, Voice, QuickSettings, PreviousApp, NextApp }
    public enum DesktopAction { None, Desktop, TaskView, Back, VoiceTyping, QuickSettings, PreviousApp, NextApp, Start, Search }

    // Pure state machine: screen bounds may have negative origins on secondary monitors.
    public sealed class GestureRecognizer
    {
        private Point start, last;
        private Rectangle screen;
        private long began, lastMotion;
        private bool bottom, top, left, right, corner;
        private Gesture latched;
        public bool Active { get; private set; }
        public int EdgeWidth = 12, Distance = 90, HoldMilliseconds = 450;
        public bool IsClick { get; private set; }

        public bool Begin(Point point, Rectangle bounds, long now)
        {
            Cancel();
            if (!bounds.Contains(point)) return false;
            bottom = point.Y >= bounds.Bottom - EdgeWidth;
            top = point.Y < bounds.Top + EdgeWidth;
            left = point.X < bounds.Left + EdgeWidth;
            right = point.X >= bounds.Right - EdgeWidth;
            if (!(bottom || top || left || right)) return false;
            corner = bottom && (point.X < bounds.Left + 60 || point.X >= bounds.Right - 60);
            screen = bounds; start = last = point; began = lastMotion = now;
            latched = Gesture.None; Active = true; IsClick = true;
            return true;
        }

        public void Move(Point point, long now)
        {
            if (!Active) return;
            if (Math.Abs(point.X - start.X) > 10 || Math.Abs(point.Y - start.Y) > 10) IsClick = false;
            // Accumulate slow movement too; don't mistake it for a stationary hold.
            if (Math.Abs(point.X - last.X) > 5 || Math.Abs(point.Y - last.Y) > 5)
            { lastMotion = now; last = point; latched = Gesture.None; }
            EvaluateHold(point, now);
        }

        public void EvaluateHold(Point point, long now)
        {
            if (!Active || now - began > 5000) return;
            int dx = point.X - start.X, up = start.Y - point.Y;
            if (bottom && up >= Distance && Math.Abs(dx) < Distance * 0.7 && now - lastMotion >= HoldMilliseconds)
                latched = Gesture.Recent;
        }

        public Gesture End(Point point, long now)
        {
            if (!Active) return Gesture.None;
            Move(point, now);
            Active = false;
            if (now - began > 5000) return Gesture.None;
            int dx = point.X - start.X, dy = point.Y - start.Y;
            if (corner && -dy >= Distance && Math.Abs(dx) >= Distance * 0.65 &&
                ((start.X < screen.Left + 60 && dx > 0) || (start.X >= screen.Right - 60 && dx < 0)))
                return Gesture.Voice;
            if (latched != Gesture.None) return latched;
            if (bottom && Math.Abs(dx) >= Distance && Math.Abs(dy) <= EdgeWidth * 3)
                return dx > 0 ? Gesture.NextApp : Gesture.PreviousApp;
            if (bottom && -dy >= Distance && Math.Abs(dx) < Distance * 0.7) return Gesture.Home;
            if (top && dy >= Distance && Math.Abs(dx) < Distance * 0.7) return Gesture.QuickSettings;
            if (((left && dx >= Distance) || (right && -dx >= Distance)) && Math.Abs(dy) < Distance * 0.7)
                return Gesture.Back;
            return Gesture.None;
        }

        public void Cancel() { Active = false; latched = Gesture.None; IsClick = false; }
    }
}
