using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace EdgeMotion
{
    internal sealed class MainForm : Form
    {
        private readonly Settings settings;
        private MouseHook hook;
        private readonly NotifyIcon tray;
        private readonly ToggleSwitch enabled, test;
        private readonly Label status, mode, saveState;
        private readonly ListBox history;
        private readonly ToolStripMenuItem pauseItem;
        private readonly Panel pages;
        private readonly FlowLayoutPanel gesturePage;
        private readonly List<GlassCard> gestureCards = new List<GlassCard>();
        private readonly List<Panel> pageList = new List<Panel>();
        private readonly List<SoftButton> nav = new List<SoftButton>();
        private bool exit, hotkeyRegistered;
        private long lastNotification;

        public MainForm(Settings settings, bool preview = false)
        {
            this.settings=settings; Text="EdgeMotion · жесты для Windows"; ClientSize=new Size(1160,850);
            MinimumSize=new Size(920,720); StartPosition=FormStartPosition.CenterScreen;
            AutoScaleMode=AutoScaleMode.Dpi; Font=new Font("Segoe UI",10); BackColor=Theme.Background; Icon=SystemIcons.Application;
            var sidebar=new Panel { Dock=DockStyle.Left,Width=210,BackColor=Color.White,Padding=new Padding(20) };
            var brand=Theme.Label("EdgeMotion",22,true,Theme.Ink); brand.Location=new Point(22,36);
            var caption=Theme.Label("WINDOWS В ДВИЖЕНИИ",8,true,Theme.Muted); caption.Location=new Point(24,82);
            sidebar.Controls.AddRange(new Control[] {brand,caption});
            string[] names={"Жесты и действия","Чувствительность","История и помощь"};
            for (int i=0;i<names.Length;i++)
            {
                int index=i; var button=new SoftButton { Text=names[i],Width=174,Height=46,Location=new Point(18,143+i*56),BackColor=Color.White,Accent=i==0 };
                button.Click += delegate { SelectPage(index); }; sidebar.Controls.Add(button); nav.Add(button);
            }
            var tip=Theme.Label("Правой кнопкой от края\n\nEsc — отмена\nCtrl + Alt + G — пауза",9,false,Theme.Muted); tip.Location=new Point(24,355); sidebar.Controls.Add(tip);
            var version=Theme.Label("Версия 0.2.0",9,false,Theme.Muted); version.Dock=DockStyle.Bottom; version.Height=30; sidebar.Controls.Add(version);
            var content=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(26,20,26,18),ColumnCount=1,RowCount=4};
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute,90)); content.RowStyles.Add(new RowStyle(SizeType.Absolute,116));
            content.RowStyles.Add(new RowStyle(SizeType.Percent,100)); content.RowStyles.Add(new RowStyle(SizeType.Absolute,78));
            Controls.Add(content); Controls.Add(sidebar);
            var header=new Panel { Dock=DockStyle.Fill };
            var title=Theme.Label("Управляй одним движением",25,true,Theme.Ink); title.Location=new Point(0,1);
            var subtitle=Theme.Label("Зажми правую кнопку у края, проведи мышью и отпусти.",10,false,Theme.Muted); subtitle.Location=new Point(2,48);
            header.Controls.AddRange(new Control[] {title,subtitle}); content.Controls.Add(header,0,0);
            var hero=new GlassCard { Dock=DockStyle.Fill,Margin=new Padding(0,0,0,16) };
            var heroLayout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=4,RowCount=2,BackColor=Color.White};
            heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,65));
            heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,65));
            heroLayout.RowStyles.Add(new RowStyle(SizeType.Percent,50)); heroLayout.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            enabled=new ToggleSwitch {Checked=true,AccessibleName="Управление жестами",BackColor=Color.White};
            test=new ToggleSwitch {Checked=settings.TestMode,AccessibleName="Режим проверки",BackColor=Color.White};
            heroLayout.Controls.Add(Theme.Label("Управление жестами",12,true,Theme.Ink),0,0); heroLayout.Controls.Add(enabled,1,0);
            heroLayout.Controls.Add(Theme.Label("Режим проверки",12,true,Theme.Ink),2,0); heroLayout.Controls.Add(test,3,0);
            mode=Theme.Label("Включено · готово к жестам",9,false,Theme.Muted); heroLayout.Controls.Add(mode,0,1); heroLayout.SetColumnSpan(mode,2);
            var safe=Theme.Label("Распознавать, не выполнять действия",9,false,Theme.Muted); heroLayout.Controls.Add(safe,2,1); heroLayout.SetColumnSpan(safe,2);
            hero.Controls.Add(heroLayout); content.Controls.Add(hero,0,1);
            pages=new Panel {Dock=DockStyle.Fill}; content.Controls.Add(pages,0,2);
            gesturePage=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,WrapContents=true,Padding=new Padding(0,2,0,4)};
            pageList.Add(gesturePage); pages.Controls.Add(gesturePage);
            AddGesture(Gesture.Home,"Снизу вверх"); AddGesture(Gesture.Recent,"Снизу вверх и остановиться");
            AddGesture(Gesture.Back,"Слева или справа к центру"); AddGesture(Gesture.Voice,"По диагонали от нижнего угла");
            AddGesture(Gesture.QuickSettings,"От верхнего края вниз"); AddGesture(Gesture.PreviousApp,"Влево вдоль нижнего края"); AddGesture(Gesture.NextApp,"Вправо вдоль нижнего края");
            gesturePage.Resize += delegate { ResizeCards(); };
            var sensitivity=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Visible=false};
            pageList.Add(sensitivity); pages.Controls.Add(sensitivity);
            AddNumber(sensitivity,"Зона у края", "Начинай жест в этой полосе. Шире — проще попасть.",4,60,settings.EdgeWidth,"пикселей",delegate(int value) {settings.EdgeWidth=value;});
            AddNumber(sensitivity,"Длина свайпа","На сколько нужно сдвинуть мышь. Меньше — короче движение.",30,400,settings.Distance,"пикселей",delegate(int value) {settings.Distance=value;});
            AddNumber(sensitivity,"Удержание для «Недавних»","После свайпа вверх останови мышь на это время и отпусти кнопку.",200,1500,settings.HoldMilliseconds,"мс",delegate(int value) {settings.HoldMilliseconds=value;});
            var fullCard=new GlassCard {Size=new Size(790,104),Margin=new Padding(0,0,0,12)};
            var full= new ToggleSwitch {Checked=settings.SkipFullscreen,Location=new Point(705,19),Anchor=AnchorStyles.Top|AnchorStyles.Right,BackColor=Color.White,AccessibleName="Пропускать полноэкранные приложения"};
            var fullTitle=Theme.Label("Пауза в полноэкранных приложениях",12,true,Theme.Ink); fullTitle.Location=new Point(20,18);
            var fullDetail=Theme.Label("Чтобы жесты не мешали играм и видео. Размер окна определяется автоматически.",9,false,Theme.Muted); fullDetail.Location=new Point(20,56); fullDetail.MaximumSize=new Size(660,0);
            full.CheckedChanged += delegate {settings.SkipFullscreen=full.Checked; Dirty();}; fullCard.Controls.AddRange(new Control[] {fullTitle,fullDetail,full}); sensitivity.Controls.Add(fullCard);
            sensitivity.Resize += delegate { foreach (Control card in sensitivity.Controls) card.Width=Math.Max(620,sensitivity.ClientSize.Width-24); };
            var help=new TableLayoutPanel {Dock=DockStyle.Fill,Visible=false,ColumnCount=1,RowCount=3};
            help.RowStyles.Add(new RowStyle(SizeType.Absolute,128)); help.RowStyles.Add(new RowStyle(SizeType.Percent,100)); help.RowStyles.Add(new RowStyle(SizeType.Absolute,64));
            pageList.Add(help); pages.Controls.Add(help);
            var helpCard=new GlassCard {Dock=DockStyle.Fill};
            var helpText=Theme.Label("Если буквы начали запускать команды",14,true,Theme.Ink); helpText.Location=new Point(18,16);
            var helpDetail=Theme.Label("Отпусти все клавиши и нажми «Восстановить ввод».\nЖесты останутся на паузе. Клавиатуру переподключать не нужно.\nНазад работает в браузере и Проводнике; голосовой ввод — в текстовом поле.",10,false,Theme.Muted); helpDetail.Location=new Point(18,49); helpCard.Controls.AddRange(new Control[] {helpText,helpDetail}); help.Controls.Add(helpCard,0,0);
            history=new ListBox {Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink,Font=new Font("Segoe UI",10),IntegralHeight=false,Margin=new Padding(0,16,0,12)}; help.Controls.Add(history,0,1);
            var recover=new SoftButton {Text="Восстановить ввод",Width=200,Accent=true}; recover.Click += delegate { Recover(); }; help.Controls.Add(recover,0,2);
            var footer=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=2,Padding=new Padding(0,12,0,0)};
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,165)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,190));
            status=Theme.Label("Сначала проверь жесты в режиме проверки.",9,false,Theme.Muted); status.AutoSize=false; status.Dock=DockStyle.Fill; footer.Controls.Add(status,0,0); footer.SetColumnSpan(status,3);
            saveState=Theme.Label("Настройки сохранены",9,false,Theme.Muted); footer.Controls.Add(saveState,0,1);
            var hide=new SoftButton {Text="Свернуть в трей",Width=155}; hide.Click += delegate {Hide();}; footer.Controls.Add(hide,1,1);
            var save=new SoftButton {Text="Сохранить настройки",Accent=true,Width=185}; save.Click += delegate {Save();}; footer.Controls.Add(save,2,1); content.Controls.Add(footer,0,3);
            var menu=new ContextMenuStrip(); menu.Items.Add("Настройки",null,delegate {ShowSettings();});
            pauseItem=new ToolStripMenuItem("Приостановить",null,delegate {enabled.Checked=!enabled.Checked;}); menu.Items.Add(pauseItem);
            menu.Items.Add("Восстановить ввод и поставить на паузу",null,delegate {Recover();}); menu.Items.Add("Выход",null,delegate {Exit();});
            tray=new NotifyIcon {Icon=Icon,Text="EdgeMotion",Visible=!preview,ContextMenuStrip=menu}; tray.DoubleClick += delegate {ShowSettings();};
            enabled.CheckedChanged += delegate {if(hook!=null)hook.Enabled=enabled.Checked; UpdateState();};
            test.CheckedChanged += delegate {settings.TestMode=test.Checked; Dirty(); UpdateState();};
            Shown += delegate
            {
                if(preview)return;
                try
                {
                    hook=new MouseHook(this,settings); hook.Recognized += OnGesture;
                    hook.Error += delegate(string message) {enabled.Checked=false; Log(message);};
                    hotkeyRegistered=Native.RegisterHotKey(Handle,1,0x4000|0x1|0x2,0x47);
                    if(!hotkeyRegistered)Log("Ctrl+Alt+G занят. Пауза доступна в трее.");
                    if(Settings.LoadWarning!=null)Log(Settings.LoadWarning);
                }
                catch(Exception ex) {enabled.Checked=false; Log("Не удалось включить жесты: "+ex.Message);}
            };
            FormClosing += delegate(object sender,FormClosingEventArgs args) {if(!exit && args.CloseReason==CloseReason.UserClosing){args.Cancel=true;Hide();}};
            SelectPage(0); UpdateState();
        }
        private void AddGesture(Gesture gesture,string direction)
        {
            var card=new GlassCard {Size=new Size(425,155),Margin=new Padding(0,0,12,12)}; gestureCards.Add(card);
            var diagram=new GestureDiagram(gesture) {Location=new Point(12,14)};
            var title=Theme.Label(ActionCatalog.GestureName(gesture),13,true,Theme.Ink); title.Location=new Point(102,15);
            var detail=Theme.Label(direction,9,false,Theme.Muted); detail.Location=new Point(102,45); detail.MaximumSize=new Size(270,0);
            var action=new SoftButton {Text=ActionCatalog.Find(settings.ActionFor(gesture)).Title+"  ▾",Location=new Point(102,76),Width=300,Height=36,BackColor=Color.White};
            var description=Theme.Label(ActionCatalog.Find(settings.ActionFor(gesture)).Detail,9,false,Theme.Muted); description.AutoSize=false; description.Location=new Point(20,119); description.Size=new Size(380,28);
            action.AccessibleName="Действие жеста "+ActionCatalog.GestureName(gesture);
            action.Click += delegate
            {
                using(var picker=new ActionPicker(ActionCatalog.GestureName(gesture),settings.ActionFor(gesture)))
                {
                    if(picker.ShowDialog(this)!=DialogResult.OK)return;
                    settings.SetAction(gesture,picker.Selected); var option=ActionCatalog.Find(picker.Selected);
                    action.Text=option.Title+"  ▾"; description.Text=option.Detail; Dirty();
                }
            };
            card.Resize += delegate {action.Width=Math.Max(150,card.Width-122); description.Width=card.Width-40;};
            card.Controls.AddRange(new Control[] {diagram,title,detail,action,description}); gesturePage.Controls.Add(card);
        }
        private void AddNumber(FlowLayoutPanel page,string title,string detail,int min,int max,int value,string unit,Action<int> changed)
        {
            var card=new GlassCard {Size=new Size(790,114),Margin=new Padding(0,0,0,12)};
            var name=Theme.Label(title,13,true,Theme.Ink); name.Location=new Point(20,16);
            var description=Theme.Label(detail,9,false,Theme.Muted); description.Location=new Point(20,49); description.MaximumSize=new Size(580,0);
            var number=new NumericUpDown {Minimum=min,Maximum=max,Value=value,Width=95,Location=new Point(650,17),Anchor=AnchorStyles.Top|AnchorStyles.Right,Font=new Font("Segoe UI",12)};
            var units=Theme.Label(unit,9,false,Theme.Muted); units.Location=new Point(650,54); units.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            card.Resize += delegate {description.MaximumSize=new Size(Math.Max(260,card.Width-200),0);};
            number.ValueChanged += delegate {changed((int)number.Value); Dirty();}; card.Controls.AddRange(new Control[] {name,description,number,units}); page.Controls.Add(card);
        }
        private void ResizeCards(){int width=gesturePage.ClientSize.Width-22; bool two=width>=760; foreach(var card in gestureCards)card.Width=two ? (width-24)/2 : width-12;}
        internal void PreparePreview(int index, bool compact) {if(compact)ClientSize=new Size(920,720);SelectPage(Math.Max(0,Math.Min(2,index)));}
        private void SelectPage(int index){for(int i=0;i<pageList.Count;i++){pageList[i].Visible=i==index; nav[i].Accent=i==index; nav[i].Invalidate();} pageList[index].BringToFront(); ResizeCards();}
        private void Dirty(){saveState.Text="Есть несохранённые изменения"; saveState.ForeColor=Theme.Blue;}
        private void Save(){try{settings.Save(); saveState.Text="Настройки сохранены";saveState.ForeColor=Theme.Muted;Log("Настройки сохранены.");}catch(Exception ex){Log("Ошибка сохранения: "+ex.Message);}}
        private void Recover(){enabled.Checked=false; System.Threading.ThreadPool.QueueUserWorkItem(delegate {try{Native.RecoverModifiers(); PostLog("Win, Alt, Ctrl и Shift отпущены. Жесты на паузе.");}catch(Exception ex){PostLog(ex.Message);}});}
        private void PostLog(string text){try{if(!IsDisposed && IsHandleCreated)BeginInvoke(new Action(delegate {Log(text);}));}catch(InvalidOperationException){}}
        private void OnGesture(Gesture gesture)
        {
            if(!enabled.Checked)return;
            string message=ActionCatalog.GestureName(gesture)+" → "+ActionCatalog.Find(settings.ActionFor(gesture)).Title;
            Log((settings.TestMode ? "Проверка: " : "Выполнено: ")+message);
            long now=Environment.TickCount & int.MaxValue;
            if(!Visible && settings.TestMode && now-lastNotification>2500){lastNotification=now;tray.ShowBalloonTip(1200,"EdgeMotion · проверка",message,ToolTipIcon.Info);}
        }
        private void UpdateState(){mode.Text=enabled.Checked ? (test.Checked ? "Проверка · действия не выполняются" : "Включено · готово к жестам") : "Пауза · обычное управление мышью";pauseItem.Text=enabled.Checked ? "Приостановить" : "Включить";tray.Text=!enabled.Checked ? "EdgeMotion · пауза" : test.Checked ? "EdgeMotion · проверка жестов" : "EdgeMotion · управление включено";}
        private void Log(string text){status.Text=text;history.Items.Insert(0,DateTime.Now.ToString("HH:mm:ss")+"  "+text);while(history.Items.Count>50)history.Items.RemoveAt(history.Items.Count-1);}
        private void ShowSettings(){Show();WindowState=FormWindowState.Normal;Activate();}
        private void Exit(){exit=true;Close();}
        protected override void WndProc(ref Message message){if(message.Msg==0x312 && message.WParam.ToInt32()==1)enabled.Checked=!enabled.Checked;base.WndProc(ref message);}
        protected override void Dispose(bool disposing){if(disposing){if(hotkeyRegistered)Native.UnregisterHotKey(Handle,1);if(hook!=null)hook.Dispose();if(tray!=null){tray.Visible=false;tray.Dispose();}}base.Dispose(disposing);}
    }
}
