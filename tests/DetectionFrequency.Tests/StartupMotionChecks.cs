using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexContinue;

static class StartupMotionChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    public static void Sequence()
    {
        var context = SynchronizationContext.Current;
        var theme = ThemeManager.Mode;
        var language = Loc.Current;
        try
        {
            foreach (var (width, height, dark) in new[] { (640, 480, false), (1120, 730, false), (1600, 900, true) })
            {
                var main = Create(width, height, dark);
                try
                {
                    var root = Find<Grid>(main, "LayoutRoot");
                    var splash = Find<TextBlock>(main, "SplashTitle");
                    var name = Find<TextBlock>(main, "TitleName");
                    var icon = Find<Image>(main, "TitleIcon");
                    var target = HeaderBounds(main);
                    var source = Bounds(splash, root);
                    Capture(root, $"startup-{width}-before.png");
                    Invoke(main, "BeginConnect");
                    FindTimer(main).Stop();
                    main.UpdateLayout();
                    Require(!Field<bool>(main, "_armed"), "Starting the connection arms a continuation");
                    if (!SystemParameters.ClientAreaAnimation) { AssertSettled(main, target); continue; }
                    Require(Near(Bounds(name, root), source, 1.5),
                        $"The shared title jumps to the corner instead of starting at the splash: source={source}, actual={Bounds(name, root)}, transform={name.RenderTransform.Value}, active={Field<bool>(main, "_startupTransitionActive")}, story={Field<Storyboard?>(main, "_startupTransition")}");
                    Require(splash.Opacity == 0 && name.Visibility == Visibility.Visible && name.CacheMode == null,
                        "The title is duplicated or magnifies a cached small bitmap");
                    var story = Field<Storyboard>(main, "_startupTransition");
                    var opened = Field<DateTime>(main, "_connectOpenedAt");
                    Invoke(main, "BeginConnect");
                    Require(ReferenceEquals(story, Field<Storyboard>(main, "_startupTransition")) && Field<DateTime>(main, "_connectOpenedAt") == opened,
                        "Repeated Start resets an in-flight title or connection deadline");
                    Pump(30); // Start the root clock before issuing interactive seek/pause requests.
                    story.Pause(main); Pump(25);
                    Seek(main, story, 0);
                    Capture(root, $"startup-{width}-000.png");
                    Seek(main, story, 160);
                    var moving = Bounds(name, root);
                    Require(moving.Left < source.Left && moving.Left > target.Left && moving.Width < source.Width && moving.Width > target.Width,
                        $"The title does not continuously move and shrink: source={source}, moving={moving}, target={target}, transform={name.RenderTransform.Value}");
                    Require(icon.Opacity < .01 && Find<Grid>(main, "ConnectHeading").Opacity < .01,
                        "The logo/content appears before the title transfer establishes the new screen");
                    bool opaqueCanvas = ((SolidColorBrush)Find<Grid>(main, "ConnectPage").Background).Color.A == 255;
                    bool hiddenDashboard = Find<Grid>(main, "TabRow").Visibility == Visibility.Collapsed &&
                        Find<Grid>(main, "ContentHost").Visibility == Visibility.Collapsed;
                    Require(Find<Grid>(main, "ConnectPage").Opacity == 1 && (opaqueCanvas || hiddenDashboard) &&
                        Panel.GetZIndex(Find<Grid>(main, "TitleBar")) > Panel.GetZIndex(Find<Grid>(main, "SplashPage")),
                        "The moving title is covered or the home page flashes through a translucent canvas");
                    Capture(root, $"startup-{width}-160.png");
                    Seek(main, story, 450);
                    double earlyProgress = (source.Width - moving.Width) / (source.Width - target.Width);
                    double middleProgress = (source.Width - Bounds(name, root).Width) / (source.Width - target.Width);
                    Require((middleProgress - earlyProgress) / 290 > earlyProgress / 160,
                        "The title does not start slowly and accelerate as requested");
                    Capture(root, $"startup-{width}-450.png");
                    Seek(main, story, 650);
                    Require(Find<Grid>(main, "ConnectHeading").Opacity > 0 && Find<Grid>(main, "ConnectHeading").Opacity < 1,
                        "The connection heading does not join through a staged reveal");
                    Require(icon.Opacity > 0 && icon.Opacity < 1, "The vector logo has no entrance transition");
                    Capture(root, $"startup-{width}-650.png");
                    if (width == 1120) { main.Width = 800; main.Height = 650; Pump(50); }
                    story.Resume(main); Seek(main, story, 1200); Pump(80);
                    AssertSettled(main, target);
                    Capture(root, $"startup-{width}-complete.png");
                    Invoke(main, "BeginConnect"); FindTimer(main).Stop();
                    Require(Near(Bounds(name, root), target, 1) && Field<Storyboard?>(main, "_startupTransition") == null,
                        "Retry replays the splash-to-header transfer");
                }
                finally { Close(main); }
            }

            var interrupted = Create(1120, 730, false, early: true);
            try
            {
                var target = HeaderBounds(interrupted);
                double alpha = Find<TextBlock>(interrupted, "SplashTitle").Opacity;
                Invoke(interrupted, "BeginConnect"); FindTimer(interrupted).Stop();
                Require(Find<TextBlock>(interrupted, "TitleName").Opacity >= alpha - .02,
                    "Starting during splash entry drops the visible title");
                Invoke(interrupted, "ShowUpdateDetails", UpdateRelease.Preview); Pump(300);
                AssertSettled(interrupted, target);
                Require(Find<Grid>(interrupted, "UpdatePage").Visibility == Visibility.Visible,
                    "Interrupting the title hides the release details");
                Pump(600);
                Require(Find<Grid>(interrupted, "UpdatePage").Visibility == Visibility.Visible,
                    "A stale startup completion overwrites release details");
            }
            finally { Close(interrupted); }

            var skipped = Create(640, 480, true);
            try
            {
                Invoke(skipped, "BeginConnect"); FindTimer(skipped).Stop();
                Invoke(skipped, "SkipLink_Click", skipped, new System.Windows.Input.MouseButtonEventArgs(
                    System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left));
                Pump(650);
                Require(Find<Grid>(skipped, "ConnectPage").Visibility == Visibility.Collapsed &&
                    Field<Storyboard?>(skipped, "_startupTransition") == null && !Field<bool>(skipped, "_armed"),
                    "Skipping leaves a title animation alive or starts a continuation");
            }
            finally { Close(skipped); }

            var settings = Create(1120, 730, false);
            try
            {
                var target = HeaderBounds(settings);
                Invoke(settings, "BeginConnect"); FindTimer(settings).Stop(); Pump(40);
                Invoke(settings, "SettingsFromTray"); Pump(500);
                AssertSettled(settings, target);
                Require(Find<Grid>(settings, "SettingsPage").Visibility == Visibility.Visible &&
                    Find<Grid>(settings, "ConnectPage").Visibility == Visibility.Collapsed,
                    "Tray settings leaves the active startup animation or connection screen above it");
            }
            finally { Close(settings); }

            var hidden = Create(1120, 730, false);
            try
            {
                var target = HeaderBounds(hidden);
                Invoke(hidden, "BeginConnect"); FindTimer(hidden).Stop(); Pump(40);
                hidden.Close(); Pump(80);
                Require(!hidden.IsVisible && Field<Storyboard?>(hidden, "_startupTransition") == null,
                    "Hiding the window leaves startup clocks running");
                Invoke(hidden, "RestoreFromTray"); Pump(80);
                AssertSettled(hidden, target);
            }
            finally { Close(hidden); }

            var instant = Create(1120, 730, false);
            try
            {
                var target = HeaderBounds(instant);
                Invoke(instant, "BeginStartupTransition", false);
                AssertSettled(instant, target);
                Invoke(instant, "SettingsFromTray"); Pump(480);
                Require(Find<Grid>(instant, "SettingsPage").Visibility == Visibility.Visible,
                    "The startup transition blocks tray settings");
            }
            finally { Close(instant); }
        }
        finally
        {
            Loc.Set(language); ThemeManager.SetMode(theme, Application.Current);
            SynchronizationContext.SetSynchronizationContext(context);
        }
    }

    static MainWindow Create(int width, int height, bool dark, bool early = false)
    {
        var main = new MainWindow { Width = width, Height = height, ShowActivated = false, Title = "Startup motion regression" };
        Find<Grid>(main, "LayoutRoot").SetResourceReference(Panel.BackgroundProperty, "CanvasBrush");
        UpdateChecks.IsolateLoadedWindow(main);
        Set(main, "_scanTask", Task.CompletedTask);
        main.Show();
        ThemeManager.SetMode(dark ? CodexContinue.ThemeMode.Dark : CodexContinue.ThemeMode.Light, Application.Current);
        Loc.Set(dark ? Loc.Lang.En : Loc.Lang.Zh); Invoke(main, "ApplyLoc");
        Invoke(main, "DismissUpdateToast"); Pump(early ? 180 : 1080);
        return main;
    }

    static Rect HeaderBounds(MainWindow main)
    {
        var name = Find<TextBlock>(main, "TitleName"); var icon = Find<Image>(main, "TitleIcon");
        name.Visibility = icon.Visibility = Visibility.Visible;
        main.UpdateLayout();
        var result = Bounds(name, Find<Grid>(main, "LayoutRoot"));
        name.Visibility = icon.Visibility = Visibility.Collapsed;
        main.UpdateLayout();
        return result;
    }

    static void AssertSettled(MainWindow main, Rect target)
    {
        var name = Find<TextBlock>(main, "TitleName");
        main.UpdateLayout();
        Require(Field<Storyboard?>(main, "_startupTransition") == null && name.RenderTransform.Value.IsIdentity &&
            Near(Bounds(name, Find<Grid>(main, "LayoutRoot")), target, 1) && name.Opacity == 1 && Find<Image>(main, "TitleIcon").Opacity == 1,
            "The title or logo does not settle cleanly at its real header anchor");
        Require(Find<Grid>(main, "SplashPage").Visibility == Visibility.Collapsed && Find<Grid>(main, "ConnectHeading").Opacity == 1,
            "The splash or an unfinished reveal remains after the transition");
    }

    static void Seek(MainWindow main, Storyboard story, int ms)
    {
        story.SeekAlignedToLastTick(main, TimeSpan.FromMilliseconds(ms), TimeSeekOrigin.BeginTime); Pump(25); main.UpdateLayout();
    }
    static Rect Bounds(FrameworkElement element, Visual root) => element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
    static bool Near(Rect a, Rect b, double tolerance) => Math.Abs(a.X - b.X) < tolerance && Math.Abs(a.Y - b.Y) < tolerance &&
        Math.Abs(a.Width - b.Width) < tolerance && Math.Abs(a.Height - b.Height) < tolerance;
    static DispatcherTimer FindTimer(MainWindow main) => Field<DispatcherTimer>(main, "_connectTimer");
    static T Find<T>(MainWindow main, string name) where T : class => main.FindName(name) as T ?? throw new Exception("Missing startup control: " + name);
    static T Field<T>(MainWindow main, string name) => (T)(typeof(MainWindow).GetField(name, Private)
        ?? throw new Exception("Missing startup field: " + name)).GetValue(main)!;
    static void Set(MainWindow main, string name, object value) => typeof(MainWindow).GetField(name, Private)!.SetValue(main, value);
    static void Invoke(MainWindow main, string method, params object[] args) => typeof(MainWindow).GetMethod(method, Private)!.Invoke(main, args);
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Close(MainWindow main) { Set(main, "_forceClose", true); main.Close(); Pump(30); }
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    static void Capture(FrameworkElement root, string name)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * 1.5), (int)Math.Ceiling(root.ActualHeight * 1.5), 144, 144, PixelFormats.Pbgra32);
        bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string folder = Path.Combine(AppContext.BaseDirectory, "Artifacts"); Directory.CreateDirectory(folder);
        using var file = File.Create(Path.Combine(folder, name)); encoder.Save(file);
    }
}
