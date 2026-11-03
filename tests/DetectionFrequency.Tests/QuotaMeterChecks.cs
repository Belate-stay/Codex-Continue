using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexContinue;

static class QuotaMeterChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run(MainWindow main)
    {
        var card = (ContentControl)main.FindName("HeroCard");
        ((Panel)card.Parent).Children.Remove(card);
        var host = new Window { Title = "Quota animation regression", Width = 900, Height = 300,
            ShowActivated = false, UseLayoutRounding = true, SnapsToDevicePixels = true, Content = card };
        var track = (Grid)main.FindName("QuotaTrack");
        var fill = (Border)main.FindName("QuotaFill");
        var pct = (TextBlock)main.FindName("QuotaPct");
        var originalTheme = ThemeManager.Mode;
        try
        {
            ThemeManager.SetMode(CodexContinue.ThemeMode.Light, Application.Current);
            host.Show();
            Pump(80);
            SetAvailable(main, 100);
            Pump(1400);
            double full = track.ActualWidth;
            Require(Math.Abs(fill.Width - full) < 0.1, "Initial quota did not settle at 100%");
            var blue = Palette("QuotaBlueBrush");
            Console.WriteLine($"Quota initial: width={fill.Width:0.##}/{full:0.##}, ink={Ink(fill)}, expected={blue}");
            Require(SameInk(Ink(fill), blue), "Initial full quota does not use the theme's blue");
            SetAvailable(main, 5);
            Require(SameInk(Ink(fill), blue),
                "Quota ink jumps to the target band before the visible fill crosses its boundary");
            var trail = main.FindName("QuotaTrail") as Border;
            Require(trail != null, "Quota meter is missing the delayed loss trail");
            SetAvailable(main, 100);
            Pump(400);

            SetAvailable(main, 60);
            Pump(90);
            Require(fill.Width < full && trail!.Visibility == Visibility.Visible && Math.Abs(trail.Width - full) < 0.1,
                "The loss trail does not preserve the old endpoint while the main fill falls");
            Capture(card, "quota-loss-light.png");
            Render(main); // The same reading must not restart the delayed trail.
            Pump(270);
            Require(trail!.Width < full && trail.Width > fill.Width,
                "The trailing fill must follow after its delay, ahead of the falling main fill");
            double linearDrop = (full - full * 0.6) * (120.0 / 1500);
            Require(full - trail.Width > linearDrop,
                "The loss trail does not start fast and decelerate");
            Pump(1500);
            Require(Math.Abs(fill.Width - full * 0.6) < 0.1 && Math.Abs(trail.Width - fill.Width) < 0.1,
                "The 100-to-60 loss did not settle both layers together");

            SetAvailable(main, 100);
            Pump(1400);
            SetAvailable(main, 80);
            Pump(90);
            double interruptedWidth = fill.Width;
            SetAvailable(main, 60);
            Require(Math.Abs(fill.Width - interruptedWidth) < 0.1 && Math.Abs(trail.Width - full) < 0.1,
                "A consecutive loss discards its original endpoint or jumps the main fill");
            Pump(1900);
            Require(Math.Abs(fill.Width - full * 0.6) < 0.1 && Math.Abs(trail.Width - fill.Width) < 0.1,
                "Consecutive losses leave an outdated trailing target");

            SetAvailable(main, 100);
            Pump(1400);
            SetAvailable(main, 5);
            Until(() => fill.Width / full <= 0.5, 1400, "Main fill never crossed 50%");
            Require(!SameInk(Ink(fill), Palette("QuotaYellowBrush")) && !SameInk(Ink(fill), Palette("QuotaRedBrush")),
                "Crossing 50% changes ink instantly");
            Pump(65);
            Require(!SameInk(Ink(fill), blue) && !SameInk(Ink(fill), Palette("QuotaYellowBrush")),
                "The blue-to-amber transition has no intermediate color");
            Capture(card, "quota-color-crossfade.png");
            Until(() => fill.Width / full < 0.1, 1400, "Main fill never crossed 10%");
            Pump(65);
            Require(!SameInk(Ink(fill), Palette("QuotaYellowBrush")) && !SameInk(Ink(fill), Palette("QuotaRedBrush")),
                "The amber-to-red transition has no intermediate color");
            Pump(1600);
            Require(SameInk(Ink(fill), Palette("QuotaRedBrush")) && Math.Abs(fill.Width - full * 0.05) < 0.1,
                "Low quota did not settle in the red band");
            Require(ReferenceEquals(fill.Background, pct.Foreground), "The main fill and percentage ink have drifted apart");

            SetAvailable(main, 100);
            Pump(1400);
            SetAvailable(main, 60);
            Pump(90);
            double beforeRecovery = fill.Width;
            SetAvailable(main, 95); // Interrupt both the old digit timer and its bar animation.
            Require(Math.Abs(fill.Width - beforeRecovery) < 0.1,
                "A newer reading jumps the main fill instead of continuing from its visible value");
            Require(trail.Visibility != Visibility.Visible || trail.Width <= fill.Width + 0.1,
                "Recovery leaves an obsolete loss trail behind");
            Pump(1500);
            Require(pct.Text == "95" && Math.Abs(fill.Width - full * 0.95) < 0.1,
                "An old completion overwrote the latest quota");

            ThemeManager.SetMode(CodexContinue.ThemeMode.Dark, Application.Current);
            Pump(60);
            Require(SameInk(Ink(fill), Palette("QuotaBlueBrush")), "Quota ink does not refresh after a theme switch");
            SetAvailable(main, 60);
            Pump(330);
            Capture(card, "quota-loss-dark.png");
            Pump(1500);
            SetAvailable(main, 0);
            Pump(1900);
            Require(fill.Width < 0.1 && trail.Width < 0.1 && pct.Text == "0", "Zero quota leaves a visible pill behind");
            foreach (double boundary in new[] { 10d, 50d })
            {
                SetAvailable(main, boundary);
                Pump(1450);
                Console.WriteLine($"Quota boundary {boundary}: ink={Ink(fill)}, expected={Palette("QuotaYellowBrush")}");
                Require(SameInk(Ink(fill), Palette("QuotaYellowBrush")),
                    $"Exactly {boundary}% is outside the 10-50 amber band");
            }
            SetAvailable(main, 10.01);
            Pump(1450);
            SetAvailable(main, 9.99);
            Pump(600);
            Require(SameInk(Ink(fill), Palette("QuotaRedBrush")),
                "A subpixel threshold crossing leaves the old color band behind");

            SetAvailable(main, 100);
            Pump(1400);
            SetAvailable(main, 60);
            Pump(90);
            SetField(main, "_snap", QuotaSnapshot.Unknown("quota animation regression"));
            SetField(main, "_conn", Enum.Parse(typeof(MainWindow).GetNestedType("ConnState", BindingFlags.NonPublic)!, "Offline"));
            Render(main);
            Require(trail.Visibility != Visibility.Visible || trail.Width < 0.1,
                "Offline state retained a misleading loss trail");
            string offlineText = pct.Text;
            Pump(1700);
            Require(pct.Text == offlineText && fill.Width < 0.1, "An old animation repainted stale quota after disconnect");

            SetAvailable(main, 30);
            Pump(80);
            Require(trail.Visibility != Visibility.Visible,
                "Initial numeric reveal is incorrectly presented as a real quota loss");
            Pump(1400);
            Require(pct.Text == "30", "Quota did not recover after disconnect");
        }
        finally
        {
            host.Close();
            ThemeManager.SetMode(originalTheme, Application.Current);
        }
    }

    static Color Ink(Border fill) => ((SolidColorBrush)fill.Background).Color;
    static bool SameInk(Color a, Color b) => a.A == b.A && a.R == b.R && a.G == b.G && a.B == b.B;
    static Color Palette(string key) => ((SolidColorBrush)Application.Current.FindResource(key)).Color;
    static void SetField(MainWindow main, string name, object value) => typeof(MainWindow).GetField(name, Private)!.SetValue(main, value);
    static void Render(MainWindow main) => typeof(MainWindow).GetMethod("Render", Private, null, Type.EmptyTypes, null)!.Invoke(main, null);
    static void SetAvailable(MainWindow main, double value)
    {
        SetField(main, "_conn", Enum.Parse(typeof(MainWindow).GetNestedType("ConnState", BindingFlags.NonPublic)!, "Online"));
        SetField(main, "_snap", new QuotaSnapshot(QuotaState.Ready, null, "quota animation regression", 100 - value));
        Render(main);
    }

    static void Capture(FrameworkElement element, string name)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),
            (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string directory = Path.Combine(AppContext.BaseDirectory, "Artifacts");
        Directory.CreateDirectory(directory);
        using var file = File.Create(Path.Combine(directory, name));
        encoder.Save(file);
    }

    static void Until(Func<bool> condition, int timeout, string message)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition() && elapsed.ElapsedMilliseconds < timeout) Pump(15);
        Require(condition(), message);
    }

    static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var stop = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        stop.Tick += (_, _) => { stop.Stop(); frame.Continue = false; };
        stop.Start();
        Dispatcher.PushFrame(frame);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
