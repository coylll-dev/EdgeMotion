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
            TestInputSafety();
            Console.WriteLine("Passed: " + passed); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void TestInputSafety()
    {
        var gate = new GestureDispatchGate(); int ticket, ignored;
        Assert(gate.TryEnter(0,out ticket), "first action accepted");
        for(int i=0;i<1000;i++) if(gate.TryEnter(i,out ignored)) throw new Exception("queued duplicate action");
        Assert(gate.IsCurrent(ticket,0,200) && !gate.IsCurrent(ticket,0,251),"late actions expire");
        gate.Cancel(); Assert(!gate.IsCurrent(ticket,0,200),"pause cancels queued actions");
        gate.Leave(); Assert(!gate.TryEnter(200,out ignored) && gate.TryEnter(300,out ticket),"rapid gesture cooldown"); gate.Leave();
        var clickGate=new GestureDispatchGate(0); clickGate.TryEnter(0,out ticket);clickGate.Leave();
        Assert(clickGate.TryEnter(1,out ticket),"ordinary clicks have no cooldown");
        int sends=0; var down=new System.Collections.Generic.HashSet<ushort>();
        var sender=new ShortcutSender(delegate(KeyStroke[] packet)
        {
            sends++; int count = sends==1 ? 2 : packet.Length;
            for(int i=0;i<count;i++) {if(packet[i].Up)down.Remove(packet[i].Key);else down.Add(packet[i].Key);}
            return (uint)count;
        },delegate(ushort key){return false;});
        bool failed=false;try{sender.Execute(new ushort[]{0x5B,0x44});}catch(InvalidOperationException){failed=true;}
        Assert(failed && down.Count==0 && sender.PendingKeys==0,"partial send releases only inserted keys");
        sends=0; bool rejectCleanup=true;
        sender=new ShortcutSender(delegate(KeyStroke[] packet)
        {
            sends++; if(sends==1)return 1;
            return rejectCleanup ? 0u : (uint)packet.Length;
        },delegate(ushort key){return false;});
        try{sender.Execute(new ushort[]{0x5B,0x44});}catch(InvalidOperationException){}
        Assert(sender.PendingKeys==1,"failed cleanup retained for watchdog");
        failed=false;try{sender.Execute(new ushort[]{0xA4,0x09});}catch(InvalidOperationException){failed=true;}
        Assert(failed && sender.PendingKeys==1,"unreleased keys block further shortcuts");
        rejectCleanup=false; sender.ReleaseOwned(); Assert(sender.PendingKeys==0,"watchdog retries cleanup");
        bool held=false; sends=0;
        sender=new ShortcutSender(delegate(KeyStroke[] packet){sends++;held=true;return 1;},delegate(ushort key){return held;});
        try{sender.Execute(new ushort[]{0x5B,0x44});}catch(InvalidOperationException){}
        Assert(sends==1 && sender.PendingKeys==1,"cleanup does not release a user's physical press");
        sends=0; sender=new ShortcutSender(delegate(KeyStroke[] packet){sends++;return (uint)packet.Length;},delegate(ushort key){return true;});
        failed=false;try{sender.Execute(new ushort[]{0x5B,0x44});}catch(InvalidOperationException){failed=true;}
        Assert(failed && sends==0,"physically held keys prevent injection");
        down.Clear(); sender=new ShortcutSender(delegate(KeyStroke[] packet)
        {
            foreach(var key in packet){if(key.Up)down.Remove(key.Key);else down.Add(key.Key);}return (uint)packet.Length;
        },delegate(ushort key){return false;});
        for(int i=0;i<10000;i++)sender.Execute(new ushort[]{0xA4,0xA0,0x09});
        Assert(down.Count==0 && sender.PendingKeys==0,"10000 fast shortcuts leave no pressed keys");
        foreach(var item in ActionCatalog.All)Assert(ActionCatalog.Find(item.Value).Title==item.Title,"action description "+item.Value);
    }
}
