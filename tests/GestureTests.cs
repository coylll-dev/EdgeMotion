using System;
using System.Drawing;
using System.IO;
using System.Xml.Serialization;
using EdgeMotion;

internal static class GestureTests
{
    private static int passed;
    private static readonly Rectangle Screen = new Rectangle(0, 0, 1920, 1080);
    private static void Assert(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
    private static Gesture Swipe(Point start, Point finish, long elapsed)
    { var r = new GestureRecognizer(); r.Begin(start, Screen, 0); r.Move(finish, 100); return r.End(finish, elapsed); }
    private static int Main()
    {
        try
        {
            Assert(Swipe(new Point(960,1079), new Point(960,850), 200) == Gesture.Home, "home");
            Assert(Swipe(new Point(960,1079), new Point(960,850), 600) == Gesture.Recent, "stationary hold");
            Assert(Swipe(new Point(0,500), new Point(140,500), 200) == Gesture.Back, "left back");
            Assert(Swipe(new Point(1919,500), new Point(1780,500), 200) == Gesture.Back, "right back");
            Assert(Swipe(new Point(960,0), new Point(960,150), 200) == Gesture.QuickSettings, "top down");
            Assert(Swipe(new Point(0,1079), new Point(150,900), 200) == Gesture.Voice, "left diagonal");
            Assert(Swipe(new Point(1919,1079), new Point(1780,900), 200) == Gesture.Voice, "right diagonal");
            Assert(Swipe(new Point(960,1079), new Point(1100,1070), 200) == Gesture.NextApp, "bottom right");
            Assert(Swipe(new Point(960,1079), new Point(800,1070), 200) == Gesture.PreviousApp, "bottom left");
            Assert(Swipe(new Point(960,1079), new Point(960,1050), 200) == Gesture.None, "short movement");
            Assert(Swipe(new Point(960,1079), new Point(960,850), 6000) == Gesture.None, "timeout");
            var r = new GestureRecognizer();
            Assert(!r.Begin(new Point(500,500), Screen, 0), "interior ignored");
            Assert(!r.Begin(new Point(-1,500), Screen, 0), "outside ignored");
            r.Begin(new Point(960,1079), Screen, 0); r.Cancel();
            Assert(r.End(new Point(960,850), 100) == Gesture.None, "cancel");
            r.Begin(new Point(960,1079), Screen, 0); r.Move(new Point(960,1078),100);
            Assert(r.End(new Point(960,1078),200) == Gesture.None && r.IsClick, "click replay eligibility");
            r.Begin(new Point(960,1079), Screen, 0); r.Move(new Point(960,850),100); r.EvaluateHold(new Point(960,850),600); r.Move(new Point(960,700),650);
            Assert(r.End(new Point(960,700),700) == Gesture.Home, "movement clears hold");
            r.Begin(new Point(960,1079), Screen, 0);
            for (int i=1; i<=50; i++) r.Move(new Point(960,1079-i*4),i*20);
            Assert(r.End(new Point(960,879),1020) == Gesture.Home, "slow movement is not hold");
            r.Begin(new Point(-960,-1), new Rectangle(-1920,-1080,1920,1080), 0); r.Move(new Point(-960,-160),100);
            Assert(r.End(new Point(-960,-160),200) == Gesture.Home, "negative monitor coordinates");
            Assert(Swipe(new Point(960,1079),new Point(1250,800),200) == Gesture.None, "ambiguous diagonal rejected");
            var settings = new Settings { Distance = 123, Home = DesktopAction.Start, TestMode = false };
            var serializer = new XmlSerializer(typeof(Settings));
            using (var stream = new MemoryStream()) { serializer.Serialize(stream,settings); stream.Position=0; var restored=(Settings)serializer.Deserialize(stream); Assert(restored.Distance==123 && restored.ActionFor(Gesture.Home)==DesktopAction.Start && !restored.TestMode,"settings XML roundtrip"); }
            Console.WriteLine("Passed: " + passed); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
