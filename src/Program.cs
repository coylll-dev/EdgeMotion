using System;
using System.Threading;
using System.Windows.Forms;

namespace EdgeMotion
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "Local\\EdgeMotion.Desktop", out created))
            {
                if (!created) { MessageBox.Show("EdgeMotion уже запущен. Откройте настройки через значок в трее.", "EdgeMotion"); return; }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                using (var form = new MainForm(Settings.Load())) Application.Run(form);
                mutex.ReleaseMutex();
            }
        }
    }
}
