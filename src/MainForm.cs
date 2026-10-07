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
        private readonly CheckBox enabled, test, fullscreen;
        private readonly NumericUpDown edge, distance, hold;
        private readonly Label status;
        private readonly ListBox history;
        private readonly ToolStripMenuItem pauseItem;
        private readonly Dictionary<Gesture, ComboBox> bindings = new Dictionary<Gesture, ComboBox>();
        private bool exit, hotkeyRegistered;
        internal static readonly Dictionary<DesktopAction, string> ActionNames = new Dictionary<DesktopAction, string>
        {
            {DesktopAction.None, "Отключено"}, {DesktopAction.Desktop, "Рабочий стол · Win+D"},
            {DesktopAction.TaskView, "Представление задач · Win+Tab"}, {DesktopAction.Back, "Назад · Alt+←"},
            {DesktopAction.VoiceTyping, "Голосовой ввод · Win+H"}, {DesktopAction.QuickSettings, "Быстрые настройки · Win+A"},
            {DesktopAction.PreviousApp, "Предыдущее окно · Alt+Tab"}, {DesktopAction.NextApp, "Обратный обход · Alt+Shift+Tab"},
            {DesktopAction.Start, "Пуск · Win"}, {DesktopAction.Search, "Поиск · Win+S"}
        };

        public MainForm(Settings settings)
        {
            this.settings = settings;
            Text = "EdgeMotion — жесты для Windows"; ClientSize = new Size(800, 780);
            MinimumSize = new Size(820, 820); StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(245, 247, 251);
            Icon = SystemIcons.Application;
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, AutoScroll = true };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            Controls.Add(panel);
            AddFull(panel, new Label { Text = "EdgeMotion", Font = new Font("Segoe UI", 25, FontStyle.Bold), ForeColor = Color.FromArgb(40, 70, 180), AutoSize = true });
            AddFull(panel, new Label { Text = "Зажмите правую кнопку у края → проведите → отпустите.\nДля «Недавних» после свайпа вверх остановитесь на 450 мс.\nEsc отменяет жест. Ctrl+Alt+G включает или приостанавливает управление.", AutoSize = true, Margin = new Padding(0, 8, 0, 14) });
            enabled = new CheckBox { Text = "Управление включено", Checked = true, AutoSize = true };
            test = new CheckBox { Text = "Проверка без выполнения действий", Checked = settings.TestMode, AutoSize = true };
            fullscreen = new CheckBox { Text = "Пауза в полноэкранных приложениях", Checked = settings.SkipFullscreen, AutoSize = true };
            AddFull(panel, enabled); AddFull(panel, test); AddFull(panel, fullscreen);
            edge = AddNumber(panel, "Зона у края (физические пиксели)", 4, 60, settings.EdgeWidth);
            distance = AddNumber(panel, "Длина свайпа (физические пиксели)", 30, 400, settings.Distance);
            hold = AddNumber(panel, "Задержка для «Недавних» (мс)", 200, 1500, settings.HoldMilliseconds);
            AddBinding(panel, Gesture.Home, "↑ От нижнего края · Домой");
            AddBinding(panel, Gesture.Recent, "↑ + остановка · Недавние");
            AddBinding(panel, Gesture.Back, "→ / ← От бокового края · Назад");
            AddBinding(panel, Gesture.Voice, "↗ / ↖ От нижнего угла · Голос");
            AddBinding(panel, Gesture.QuickSettings, "↓ От верхнего края · Шторка");
            AddBinding(panel, Gesture.PreviousApp, "← Вдоль нижнего края");
            AddBinding(panel, Gesture.NextApp, "→ Вдоль нижнего края");
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 14, 0, 8) };
            var save = new Button { Text = "Сохранить настройки", AutoSize = true };
            var hide = new Button { Text = "Свернуть в трей", AutoSize = true };
            var quit = new Button { Text = "Выход", AutoSize = true };
            buttons.Controls.AddRange(new Control[] { save, hide, quit }); AddFull(panel, buttons);
            status = new Label { Text = "Готово. Режим проверки можно выключить после настройки.", AutoSize = true, ForeColor = Color.FromArgb(40, 70, 180) }; AddFull(panel, status);
            history = new ListBox { Height = 105, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) }; AddFull(panel, history);
            var menu = new ContextMenuStrip();
            menu.Items.Add("Настройки", null, delegate { ShowSettings(); });
            pauseItem = new ToolStripMenuItem("Приостановить", null, delegate { enabled.Checked = !enabled.Checked; }); menu.Items.Add(pauseItem);
            menu.Items.Add("Выход", null, delegate { Exit(); });
            tray = new NotifyIcon { Icon = Icon, Text = "EdgeMotion · проверка жестов", Visible = true, ContextMenuStrip = menu };
            tray.DoubleClick += delegate { ShowSettings(); };
            enabled.CheckedChanged += delegate { if (hook != null) hook.Enabled = enabled.Checked; pauseItem.Text = enabled.Checked ? "Приостановить" : "Включить"; UpdateTray(); };
            test.CheckedChanged += delegate { settings.TestMode = test.Checked; UpdateTray(); };
            fullscreen.CheckedChanged += delegate { settings.SkipFullscreen = fullscreen.Checked; };
            edge.ValueChanged += delegate { settings.EdgeWidth = (int)edge.Value; };
            distance.ValueChanged += delegate { settings.Distance = (int)distance.Value; };
            hold.ValueChanged += delegate { settings.HoldMilliseconds = (int)hold.Value; };
            save.Click += delegate { try { settings.Save(); Log("Настройки сохранены."); } catch (Exception ex) { Log("Ошибка сохранения: " + ex.Message); } };
            hide.Click += delegate { Hide(); }; quit.Click += delegate { Exit(); };
            Shown += delegate
            {
                try
                {
                    hook = new MouseHook(this, settings);
                    hook.Recognized += OnGesture; hook.Error += Log;
                    hotkeyRegistered = Native.RegisterHotKey(Handle, 1, 0x4000 | 0x1 | 0x2, 0x47);
                    if (!hotkeyRegistered) Log("Ctrl+Alt+G занят. Пауза доступна в трее.");
                    if (Settings.LoadWarning != null) Log(Settings.LoadWarning);
                    UpdateTray();
                }
                catch (Exception ex) { enabled.Checked = false; Log("Не удалось включить жесты: " + ex.Message); }
            };
            FormClosing += delegate(object sender, FormClosingEventArgs args)
            {
                if (!exit && args.CloseReason == CloseReason.UserClosing) { args.Cancel = true; Hide(); }
            };
        }

        private static void AddFull(TableLayoutPanel panel, Control control)
        { int row = panel.RowCount++; panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.Controls.Add(control, 0, row); panel.SetColumnSpan(control, 2); }
        private static NumericUpDown AddNumber(TableLayoutPanel panel, string name, int min, int max, int value)
        {
            int row = panel.RowCount++; panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(new Label { Text = name, AutoSize = true, Margin = new Padding(0, 8, 0, 8) }, 0, row);
            var number = new NumericUpDown { Minimum = min, Maximum = max, Value = value, Width = 120, Margin = new Padding(0, 4, 0, 4) };
            panel.Controls.Add(number, 1, row); return number;
        }
        private void AddBinding(TableLayoutPanel panel, Gesture gesture, string name)
        {
            int row = panel.RowCount++; panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(new Label { Text = name, AutoSize = true, Margin = new Padding(0, 8, 0, 8) }, 0, row);
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
            var values = new List<KeyValuePair<DesktopAction, string>>(ActionNames);
            combo.DataSource = values; combo.DisplayMember = "Value"; combo.ValueMember = "Key"; combo.SelectedValue = settings.ActionFor(gesture);
            combo.SelectedValueChanged += delegate
            {
                if (!(combo.SelectedValue is DesktopAction)) return;
                var action = (DesktopAction)combo.SelectedValue;
                switch (gesture)
                {
                    case Gesture.Home: settings.Home = action; break;
                    case Gesture.Recent: settings.Recent = action; break;
                    case Gesture.Back: settings.Back = action; break;
                    case Gesture.Voice: settings.Voice = action; break;
                    case Gesture.QuickSettings: settings.QuickSettings = action; break;
                    case Gesture.PreviousApp: settings.PreviousApp = action; break;
                    case Gesture.NextApp: settings.NextApp = action; break;
                }
            };
            bindings.Add(gesture, combo); panel.Controls.Add(combo, 1, row);
        }
        private void OnGesture(Gesture gesture)
        {
            if (!enabled.Checked) return;
            var action = settings.ActionFor(gesture);
            try
            {
                if (!settings.TestMode) Native.Execute(action);
                Log((settings.TestMode ? "Проверка: " : "Выполнено: ") + gesture + " → " + ActionNames[action]);
                if (!Visible && settings.TestMode) tray.ShowBalloonTip(1500, "EdgeMotion · проверка", gesture + " → " + ActionNames[action], ToolTipIcon.Info);
            }
            catch (Exception ex) { Log(ex.Message); tray.ShowBalloonTip(2000, "EdgeMotion", ex.Message, ToolTipIcon.Warning); }
        }
        private void UpdateTray() { tray.Text = !enabled.Checked ? "EdgeMotion · пауза" : settings.TestMode ? "EdgeMotion · проверка жестов" : "EdgeMotion · управление включено"; }
        private void Log(string text) { status.Text = text; history.Items.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + text); while (history.Items.Count > 50) history.Items.RemoveAt(history.Items.Count - 1); }
        private void ShowSettings() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        private void Exit() { exit = true; Close(); }
        protected override void WndProc(ref Message message) { if (message.Msg == 0x312 && message.WParam.ToInt32() == 1) enabled.Checked = !enabled.Checked; base.WndProc(ref message); }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { if (hotkeyRegistered) Native.UnregisterHotKey(Handle, 1); if (hook != null) hook.Dispose(); tray.Visible = false; tray.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
