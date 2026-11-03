using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using CodexContinue;
using ShapePath = System.Windows.Shapes.Path;

static class AppearanceChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Preferences()
    {
        var property = typeof(AppSettings).GetProperty("Transparency") ?? throw new Exception("Missing transparency preference");
        var legacy = JsonSerializer.Deserialize<AppSettings>("{\"Theme\":\"dark\",\"Accent\":\"pink\",\"IdleScanSeconds\":1800}")!;
        Require(Equals(property.GetValue(legacy), false) && legacy.Accent == "pink" && legacy.IdleScanSeconds == 1800,
            "Transparency is not off by default or changes existing preferences");
        property.SetValue(legacy, true); legacy.Accent = "system";
        var loaded = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(legacy))!;
        Require(Equals(property.GetValue(loaded), true) && loaded.Accent == "system", "Glass and system accent do not persist");
    }

    public static void Loop() => WithWindow(main =>
    {
        Invoke(main, "SwitchView", View("Auto")); Pump(400);
        var loop = Find<CheckBox>(main, "LoopSend"); loop.IsChecked = true; Pump(240); loop.ApplyTemplate();
        var tick = (ShapePath)loop.Template.FindName("LoopTick", loop);
        var face = (Border)loop.Template.FindName("LoopFace", loop);
        var pen = new Pen(tick.Stroke, tick.StrokeThickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var bounds = tick.TransformToAncestor(face).TransformBounds(tick.RenderedGeometry.GetRenderBounds(pen));
        Require(Math.Abs(bounds.X + bounds.Width / 2 - face.ActualWidth / 2) < .4 &&
            Math.Abs(bounds.Y + bounds.Height / 2 - face.ActualHeight / 2) < .4, $"The loop check is off-center: {bounds}, face={face.RenderSize}");
        foreach (var language in new[] { Loc.Lang.Zh, Loc.Lang.En })
        {
            Loc.Set(language); Invoke(main, "ApplyLoc");
            var text = Find<TextBlock>(main, "LoopHelpText");
            Require(!text.Text.Contains("未启用时") && !text.Text.Contains("When disabled") && text.Text.Split("\n\n").Length == 2,
                "Loop help still includes the disabled-mode paragraph or lacks readable paragraph spacing");
            Require(text.LineHeight >= 18 && text.MaxWidth <= 320, "The shorter loop help has cramped or overlong lines");
        }
    });

    public static void Material() => WithWindow(main =>
    {
        var settings = (AppSettings)typeof(MainWindow).GetField("_settings", Private)!.GetValue(main)!;
        var setter = typeof(ThemeManager).GetMethod("SetTransparency") ?? throw new Exception("Missing native transparency integration");
        Require(main.FindName("LblTransparency") != null, "Missing transparency row");
        typeof(MainWindow).GetField("_conn", Private)!.SetValue(main,
            Enum.Parse(typeof(MainWindow).GetNestedType("ConnState", BindingFlags.NonPublic)!, "Online"));
        typeof(MainWindow).GetField("_snap", Private)!.SetValue(main, new QuotaSnapshot(QuotaState.Ready, null, "material quota fixture", 80));
        typeof(MainWindow).GetMethod("Render", Private, null, Type.EmptyTypes, null)!.Invoke(main, null); Pump(1400);
        foreach (var mode in new[] { CodexContinue.ThemeMode.Light, CodexContinue.ThemeMode.Dark })
        foreach (var lang in new[] { Loc.Lang.Zh, Loc.Lang.En })
        {
            ThemeManager.SetMode(mode, Application.Current); Loc.Set(lang);
            typeof(AppSettings).GetProperty("Transparency")!.SetValue(settings, true);
            settings.Accent = "system"; ThemeManager.SetAccent("system", Application.Current, animate: false);
            setter.Invoke(null, new object[] { true, Application.Current, false });
            Invoke(main, "ApplyLoc"); Invoke(main, "SwitchView", View("Settings")); Invoke(main, "LoadSettingsSelection"); Pump(450);
            Require(main.Opacity == 1 && !main.AllowsTransparency, "Glass fades text or uses a layered window");
            Require(Find<RadioButton>(main, "TransparencyOn").IsChecked == true && Find<RadioButton>(main, "AccentSystem").IsChecked == true,
                "Settings do not reflect glass/system accent");
            var accent = Find<TextBlock>(main, "LblAccent"); var transparency = Find<TextBlock>(main, "LblTransparency"); var language = Find<TextBlock>(main, "LblLang");
            Require(accent.TranslatePoint(new Point(), main).Y < transparency.TranslatePoint(new Point(), main).Y &&
                transparency.TranslatePoint(new Point(), main).Y < language.TranslatePoint(new Point(), main).Y, "Transparency is not below Accent in General");
            bool enabled = (bool)typeof(ThemeManager).GetProperty("TransparencyEnabled")!.GetValue(null)!;
            if (enabled)
            {
                int native = 0; int result = DwmGetWindowAttribute(new WindowInteropHelper(main).Handle, 38, out native, 4);
                Require(result >= 0 && native == 3 && WindowChrome.GetWindowChrome(main).GlassFrameThickness == new Thickness(-1),
                    "The window is not using the native Desktop Acrylic material across its full frame");
                var nativeType = typeof(App).Assembly.GetType("CodexContinue.Native")!;
                long style = ((IntPtr)nativeType.GetMethod("GetWindowLongPtr")!.Invoke(null,
                    new object[] { new WindowInteropHelper(main).Handle, -16 })!).ToInt64();
                Require((style & 0x00C00000) == 0 && (style & 0x00040000) != 0 && (style & 0x00030000) == 0x00030000,
                    "Glass reveals duplicate native captions or loses native resizing/minimize/maximize flags");
                Require(((SolidColorBrush)main.Background).Color.A < 255 && ((SolidColorBrush)Find<ContentControl>(main, "GeneralSettingsCard").Background).Color.A < 255,
                    "Opaque WPF surfaces hide the native frost");
                if (mode == CodexContinue.ThemeMode.Light && lang == Loc.Lang.Zh)
                {
                    main.WindowState = WindowState.Maximized; Pump(120);
                    DwmGetWindowAttribute(new WindowInteropHelper(main).Handle, 38, out int maximizedMaterial, 4);
                    Require(maximizedMaterial == 3 && WindowChrome.GetWindowChrome(main).GlassFrameThickness == new Thickness(-1),
                        "Maximizing loses the native glass material");
                    main.WindowState = WindowState.Normal; Pump(100);
                }
            }
            foreach (double width in new[] { 640d, 1120d })
            {
                main.Width = width; Pump(80); Find<RadioButton>(main, "TransparencyOn").BringIntoView(); Pump(40);
                var scroll = Find<ScrollViewer>(main, "SettingsScroll");
                var row = Find<RadioButton>(main, "TransparencyOn");
                var point = row.TranslatePoint(new Point(), scroll);
                Require(point.X >= 0 && point.Y >= 0 && point.X + row.ActualWidth <= scroll.ActualWidth + 1 &&
                    point.Y + row.ActualHeight <= scroll.ActualHeight + 1, "The transparency choice is clipped in compact settings");
            }
            setter.Invoke(null, new object[] { false, Application.Current, true }); Pump(90);
            setter.Invoke(null, new object[] { true, Application.Current, true }); Pump(400);
            if (enabled)
            {
                DwmGetWindowAttribute(new WindowInteropHelper(main).Handle, 38, out int material, 4);
                Require(material == 3, "An interrupted fade disables the newly re-enabled native material");
            }
            typeof(AppSettings).GetProperty("Transparency")!.SetValue(settings, false);
            setter.Invoke(null, new object[] { false, Application.Current, true }); Pump(450);
            Require(((SolidColorBrush)main.Background).Color.A == 255 && WindowChrome.GetWindowChrome(main).GlassFrameThickness != new Thickness(-1),
                "Turning glass off leaves a transparent or extended frame");
            Invoke(main, "SwitchView", View("Auto")); Pump(500);
            var fill = Find<Border>(main, "QuotaFill"); var track = Find<FrameworkElement>(main, "QuotaTrack");
            Require(Math.Abs(fill.Width / track.ActualWidth * 100 - 20) < .1 &&
                Same(((SolidColorBrush)fill.Background).Color, ((SolidColorBrush)Application.Current.FindResource("QuotaYellowBrush")).Color),
                $"Material frame changes reset the cached quota width or ink: width={fill.Width}/{track.ActualWidth}, color={((SolidColorBrush)fill.Background).Color}");
        }
    });

    public static void SystemAccent() => WithWindow(main =>
    {
        ThemeManager.SetAccent("system", Application.Current, animate: false);
        var expected = ((SolidColorBrush)Application.Current.FindResource("AccentBrush")).Color;
        Application.Current.Resources["AccentBrush"] = new SolidColorBrush(Colors.Lime);
        typeof(MainWindow).GetMethod("WndProc", Private)!.Invoke(main,
            new object[] { new WindowInteropHelper(main).Handle, 0x0320, IntPtr.Zero, IntPtr.Zero, false });
        Pump(400);
        Require(Same(((SolidColorBrush)Application.Current.FindResource("AccentBrush")).Color, expected),
            $"The system colorization notification does not refresh the live accent: expected={expected}, actual={((SolidColorBrush)Application.Current.FindResource("AccentBrush")).Color}, selected={ThemeManager.Accent}");
        var resolver = typeof(ThemeManager).GetMethod("SystemPalette", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (bool dark in new[] { false, true })
        foreach (var color in new[] { Colors.Black, Colors.White, Colors.Yellow, Colors.Blue, Colors.Red, Colors.Gray, Colors.Lime, Colors.Magenta })
        {
            var palette = (Color[])resolver.Invoke(null, new object[] { color, dark })!;
            static double L(Color c)
            {
                static double P(byte n) { double v = n / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
                return .2126 * P(c.R) + .7152 * P(c.G) + .0722 * P(c.B);
            }
            double ratio = (Math.Max(L(palette[0]), L(palette[1])) + .05) / (Math.Min(L(palette[0]), L(palette[1])) + .05);
            Require(Same(palette[0], color) && ratio >= 4.5, "A system accent changes the source color or makes selected text unreadable");
        }
    });

    public static void StartupQuota()
    {
        var context = SynchronizationContext.Current; var mode = ThemeManager.Mode; var accent = ThemeManager.Accent; var lang = Loc.Current;
        var main = new MainWindow { ShowActivated = false, Title = "Glass startup quota regression" };
        UpdateChecks.IsolateLoadedWindow(main);
        typeof(MainWindow).GetField("_scanTask", Private)!.SetValue(main, Task.CompletedTask);
        try
        {
            ThemeManager.SetTransparency(true, Application.Current, animate: false);
            main.Show(); Pump(100);
            typeof(MainWindow).GetField("_conn", Private)!.SetValue(main,
                Enum.Parse(typeof(MainWindow).GetNestedType("ConnState", BindingFlags.NonPublic)!, "Online"));
            typeof(MainWindow).GetField("_prefetched", Private)!.SetValue(main, new QuotaSnapshot(QuotaState.Ready, null, "glass startup fixture", 80));
            Find<Grid>(main, "ConnectPage").Visibility = Visibility.Visible;
            Find<Grid>(main, "SplashPage").Visibility = Visibility.Collapsed;
            Invoke(main, "EnterMain"); Pump(2200);
            var fill = Find<Border>(main, "QuotaFill"); var track = Find<FrameworkElement>(main, "QuotaTrack");
            Require(Math.Abs(fill.Width / track.ActualWidth * 100 - 20) < .1 &&
                Same(((SolidColorBrush)fill.Background).Color, ((SolidColorBrush)Application.Current.FindResource("QuotaYellowBrush")).Color),
                $"Glass startup leaves the quota unmeasured: fill={fill.Width}, track={track.ActualWidth}, color={((SolidColorBrush)fill.Background).Color}");
        }
        finally
        {
            typeof(MainWindow).GetField("_forceClose", Private)!.SetValue(main, true); main.Close();
            ThemeManager.SetTransparency(false, Application.Current, animate: false); ThemeManager.SetMode(mode, Application.Current);
            ThemeManager.SetAccent(accent, Application.Current, animate: false); Loc.Set(lang);
            SynchronizationContext.SetSynchronizationContext(context);
        }
    }

    static void WithWindow(Action<MainWindow> check)
    {
        var context = SynchronizationContext.Current; var mode = ThemeManager.Mode; var accent = ThemeManager.Accent; var lang = Loc.Current;
        var main = new MainWindow { ShowActivated = false, Title = "Appearance regression" };
        UpdateChecks.IsolateLoadedWindow(main); typeof(MainWindow).GetField("_scanTask", Private)!.SetValue(main, Task.CompletedTask);
        try
        {
            main.Show(); Find<Grid>(main, "SplashPage").Visibility = Visibility.Collapsed;
            Find<TextBlock>(main, "TitleName").Visibility = Find<Image>(main, "TitleIcon").Visibility = Visibility.Visible;
            Invoke(main, "DismissUpdateToast"); Pump(150); check(main);
        }
        finally
        {
            typeof(MainWindow).GetField("_forceClose", Private)!.SetValue(main, true); main.Close();
            typeof(ThemeManager).GetMethod("SetTransparency")?.Invoke(null, new object[] { false, Application.Current, false });
            ThemeManager.SetMode(mode, Application.Current); ThemeManager.SetAccent(accent, Application.Current, animate: false); Loc.Set(lang);
            SynchronizationContext.SetSynchronizationContext(context);
        }
    }
    static object View(string name) => Enum.Parse(typeof(MainWindow).GetNestedType("ViewMode", BindingFlags.NonPublic)!, name);
    static T Find<T>(MainWindow main, string name) where T : class => (T)main.FindName(name);
    static object? Invoke(MainWindow main, string method, params object[] args) => typeof(MainWindow).GetMethod(method, Private)!.Invoke(main, args);
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static bool Same(Color a, Color b) => a.A == b.A && a.R == b.R && a.G == b.G && a.B == b.B;
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
}
