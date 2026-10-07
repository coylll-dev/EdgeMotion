using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EdgeMotion
{
    internal static class Theme
    {
        public static readonly Color Ink = Color.FromArgb(32, 40, 58), Muted = Color.FromArgb(105, 115, 135), Blue = Color.FromArgb(63, 104, 239), Background = Color.FromArgb(240, 244, 251);
        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right-d, r.Y,d,d,270,90);
            path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); path.AddArc(r.X,r.Bottom-d,d,d,90,90); path.CloseFigure(); return path;
        }
        public static Label Label(string text, int size, bool bold, Color color)
        { return new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color, BackColor = Color.Transparent }; }
    }
    internal sealed class GlassCard : Panel
    {
        public GlassCard() { DoubleBuffered = true; BackColor = Theme.Background; Padding = new Padding(18); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width < 5 || Height < 5) return;
            var r = new Rectangle(1,1,Width-3,Height-3);
            using (var path = Theme.Rounded(r,22))
            using (var fill = new LinearGradientBrush(r,Color.White,Color.FromArgb(247,250,255),65))
            using (var border = new Pen(Color.FromArgb(221,229,243)))
            { e.Graphics.FillPath(fill,path); e.Graphics.DrawPath(border,path); }
        }
    }
    internal sealed class SoftButton : Button
    {
        public bool Accent;
        private bool hover;
        public SoftButton() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; Height = 42; Font = new Font("Segoe UI",10,FontStyle.Bold); SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); }
        protected override void OnMouseEnter(EventArgs e) { hover=true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover=false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using (var path=Theme.Rounded(new Rectangle(0,0,Width-1,Height-1),13))
            using (var brush=new SolidBrush(Accent ? (hover ? Color.FromArgb(48,84,216) : Theme.Blue) : (hover ? Color.FromArgb(223,232,248) : Color.FromArgb(233,239,250))))
                e.Graphics.FillPath(brush,path);
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled ? (Accent ? Color.White : Theme.Ink) : Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(5,5,Width-10,Height-10));
        }
    }
    internal sealed class ToggleSwitch : CheckBox
    {
        public ToggleSwitch() { AutoSize=false; Size=new Size(52,30); Text=""; Cursor=Cursors.Hand; SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using (var path=Theme.Rounded(new Rectangle(1,3,48,24),12))
            using (var fill=new SolidBrush(Checked ? Theme.Blue : Color.FromArgb(199,207,222))) e.Graphics.FillPath(fill,path);
            using (var knob=new SolidBrush(Color.White)) e.Graphics.FillEllipse(knob,Checked ? 28 : 4,6,18,18);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);
        }
    }
    internal sealed class GestureDiagram : Control
    {
        private readonly Gesture gesture;
        public GestureDiagram(Gesture gesture) { this.gesture=gesture; Size=new Size(88,82); DoubleBuffered=true; BackColor=Color.White; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using (var path=Theme.Rounded(new Rectangle(12,9,62,59),10))
            using (var brush=new SolidBrush(Color.FromArgb(238,244,255))) e.Graphics.FillPath(brush,path);
            using (var border=new Pen(Color.FromArgb(195,212,243),1.5f)) e.Graphics.DrawRectangle(border,18,15,50,45);
            Point a=new Point(43,65), b=new Point(43,26);
            switch (gesture)
            {
                case Gesture.Back: a=new Point(13,38); b=new Point(53,38); break;
                case Gesture.QuickSettings: a=new Point(43,10); b=new Point(43,51); break;
                case Gesture.Voice: a=new Point(16,65); b=new Point(55,25); break;
                case Gesture.PreviousApp: a=new Point(68,65); b=new Point(23,65); break;
                case Gesture.NextApp: a=new Point(20,65); b=new Point(65,65); break;
            }
            using (var pen=new Pen(Theme.Blue,3))
            using (var arrow=new AdjustableArrowCap(4,4,true))
            { pen.CustomEndCap=arrow; e.Graphics.DrawLine(pen,a,b); }
            using (var dot=new SolidBrush(Theme.Blue)) e.Graphics.FillEllipse(dot,a.X-4,a.Y-4,8,8);
            if (gesture==Gesture.Recent) using (var pen=new Pen(Theme.Blue,2)) e.Graphics.DrawEllipse(pen,34,17,18,18);
        }
    }
}
