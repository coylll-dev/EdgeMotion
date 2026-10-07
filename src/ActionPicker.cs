using System;
using System.Drawing;
using System.Windows.Forms;

namespace EdgeMotion
{
    internal sealed class ActionPicker : Form
    {
        public DesktopAction Selected { get; private set; }
        public ActionPicker(string name, DesktopAction selected)
        {
            Text="Действие для жеста «"+name+"»"; Selected=selected;
            ClientSize=new Size(690,680); MinimumSize=new Size(650,600); StartPosition=FormStartPosition.CenterParent;
            BackColor=Theme.Background; Font=new Font("Segoe UI",10); ShowInTaskbar=false;
            var heading=Theme.Label("Что должен делать жест «"+name+"»?",19,true,Theme.Ink); heading.Dock=DockStyle.Top; heading.AutoSize=false; heading.Height=76; heading.Padding=new Padding(22,20,0,0);
            var list=new FlowLayoutPanel { Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(18,0,10,12),WrapContents=false,FlowDirection=FlowDirection.TopDown };
            foreach (var item in ActionCatalog.All)
            {
                var card=new GlassCard { Size=new Size(625,90),Margin=new Padding(0,0,0,8) };
                var radio=new RadioButton { Text=item.Title,Checked=item.Value==selected,AutoSize=true,Location=new Point(18,12),Font=new Font("Segoe UI",11,FontStyle.Bold),BackColor=Color.White,ForeColor=Theme.Ink };
                var detail=Theme.Label(item.Detail,9,false,Theme.Muted); detail.Location=new Point(40,41); detail.MaximumSize=new Size(560,0);
                var key=Theme.Label(item.Shortcut,9,false,Theme.Blue); key.Location=new Point(40,64);
                card.Controls.AddRange(new Control[] {radio,detail,key}); list.Controls.Add(card);
                radio.CheckedChanged += delegate
                {
                    if (!radio.Checked) return;
                    Selected=item.Value;
                    foreach (Control other in list.Controls) if (other!=card)
                        foreach (Control child in other.Controls) { var option=child as RadioButton; if (option!=null) option.Checked=false; }
                };
                card.Click += delegate { radio.Checked=true; }; detail.Click += delegate { radio.Checked=true; }; key.Click += delegate { radio.Checked=true; };
            }
            var footer=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=68,Padding=new Padding(18,12,0,0),FlowDirection=FlowDirection.RightToLeft };
            var apply=new SoftButton {Text="Выбрать действие",Accent=true,Width=180,DialogResult=DialogResult.OK};
            var cancel=new SoftButton {Text="Отмена",Width=100,DialogResult=DialogResult.Cancel};
            footer.Controls.AddRange(new Control[] {apply,cancel}); AcceptButton=apply; CancelButton=cancel;
            Controls.Add(list); Controls.Add(heading); Controls.Add(footer);
        }
    }
}
