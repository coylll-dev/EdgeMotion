using System;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;

namespace EdgeMotion
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length >= 2 && (args[0] == "--preview" || args[0] == "--preview-actions"))
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                using (Form preview = args[0] == "--preview-actions" ? (Form)new ActionPicker("Домой", DesktopAction.Desktop) : new MainForm(new Settings(), true))
                {
                    preview.Opacity = 0;
                    preview.Show();
                    Application.DoEvents();
                    var mainPreview = preview as MainForm;
                    int page;
                    if (mainPreview != null) mainPreview.PreparePreview(args.Length > 2 && int.TryParse(args[2], out page) ? page : 0, args.Length > 3 && args[3] == "compact");
                    preview.PerformLayout();
                    using (var bitmap = new Bitmap(preview.Width, preview.Height))
                    { preview.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(args[1], ImageFormat.Png); }
                }
                return;
            }
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
