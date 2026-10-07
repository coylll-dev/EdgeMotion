using System;
using System.IO;
using System.Xml.Serialization;

namespace EdgeMotion
{
    public class Settings
    {
        public int EdgeWidth = 12;
        public int Distance = 90;
        public int HoldMilliseconds = 450;
        public bool SkipFullscreen = true;
        public bool TestMode = true;
        public DesktopAction Home = DesktopAction.Desktop;
        public DesktopAction Recent = DesktopAction.TaskView;
        public DesktopAction Back = DesktopAction.Back;
        public DesktopAction Voice = DesktopAction.VoiceTyping;
        public DesktopAction QuickSettings = DesktopAction.QuickSettings;
        public DesktopAction PreviousApp = DesktopAction.PreviousApp;
        public DesktopAction NextApp = DesktopAction.NextApp;
        public static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EdgeMotion");
        public static readonly string FilePath = Path.Combine(DirectoryPath, "settings.xml");
        public static string LoadWarning;

        public static Settings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new Settings();
                using (var stream = File.OpenRead(FilePath))
                {
                    var result = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(stream);
                    result.EdgeWidth = Math.Max(4, Math.Min(60, result.EdgeWidth));
                    result.Distance = Math.Max(30, Math.Min(400, result.Distance));
                    result.HoldMilliseconds = Math.Max(200, Math.Min(1500, result.HoldMilliseconds));
                    return result;
                }
            }
            catch (Exception ex) { LoadWarning = "Настройки не прочитаны: " + ex.Message; return new Settings(); }
        }

        public void Save()
        {
            Directory.CreateDirectory(DirectoryPath);
            string temp = FilePath + ".tmp";
            using (var stream = File.Create(temp)) new XmlSerializer(typeof(Settings)).Serialize(stream, this);
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
        }

        public DesktopAction ActionFor(Gesture gesture)
        {
            switch (gesture)
            {
                case Gesture.Home: return Home;
                case Gesture.Recent: return Recent;
                case Gesture.Back: return Back;
                case Gesture.Voice: return Voice;
                case Gesture.QuickSettings: return QuickSettings;
                case Gesture.PreviousApp: return PreviousApp;
                case Gesture.NextApp: return NextApp;
                default: return DesktopAction.None;
            }
        }
        public void SetAction(Gesture gesture, DesktopAction action)
        {
            switch (gesture)
            {
                case Gesture.Home: Home=action; break; case Gesture.Recent: Recent=action; break;
                case Gesture.Back: Back=action; break; case Gesture.Voice: Voice=action; break;
                case Gesture.QuickSettings: QuickSettings=action; break;
                case Gesture.PreviousApp: PreviousApp=action; break; case Gesture.NextApp: NextApp=action; break;
            }
        }
    }
}
