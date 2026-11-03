using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexContinue;

static class AdaptiveWindowChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    public static void Shell()
    {
        var context = SynchronizationContext.Current;
        var main = new MainWindow { Title = "Adaptive shell regression", ShowActivated = false };
        UpdateChecks.IsolateLoadedWindow(main);
        try
        {
            Require(main.ResizeMode == ResizeMode.CanResize && double.IsPositiveInfinity(main.MaxWidth) && double.IsPositiveInfinity(main.MaxHeight),
                "The window is still fixed-size or cannot maximize");
            main.Show(); Pump(150);
            CaptionFitsTop(main);
            var original = main.RestoreBounds;
            var maximize = Find<Button>(main, "BtnMaximize");
            maximize.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Pump(300);
            Require(main.WindowState == WindowState.Maximized, "The maximize button does not maximize");
            CaptionFitsTop(main);
            IntPtr hwnd = new WindowInteropHelper(main).Handle;
            Require(AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(hwnd), new IntPtr(-4)), "The window does not use PerMonitorV2 DPI awareness");
            var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref monitor);
            GetWindowRect(hwnd, out var bounds);
            DwmGetWindowAttribute(hwnd, 9, out var visibleBounds, Marshal.SizeOf<RectI>());
            var root = Find<FrameworkElement>(main, "LayoutRoot");
            Console.WriteLine($"Maximized outer={bounds}, visible={visibleBounds}, root origin={root.PointToScreen(new Point())}, size={root.ActualWidth}x{root.ActualHeight}, dpi={VisualTreeHelper.GetDpi(root).DpiScaleX}, chrome={System.Windows.Shell.WindowChrome.GetWindowChrome(main).ResizeBorderThickness}");
            var topLeft = root.PointToScreen(new Point());
            var bottomRight = root.PointToScreen(new Point(root.ActualWidth, root.ActualHeight));
            Require(Math.Abs(topLeft.X - monitor.Work.Left) <= 2 && Math.Abs(topLeft.Y - monitor.Work.Top) <= 2 &&
                Math.Abs(bottomRight.X - monitor.Work.Right) <= 2 && Math.Abs(bottomRight.Y - monitor.Work.Bottom) <= 2,
                $"Maximized content does not fit the work area: content={topLeft}-{bottomRight}, work={monitor.Work}");
            Find<Button>(main, "BtnMin").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Pump(100);
            Invoke(main, "RestoreFromTray"); Pump(250);
            Require(main.WindowState == WindowState.Maximized, "Restoring a minimized maximized window loses its maximized state");
            maximize.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Pump(200);
            Require(main.WindowState == WindowState.Normal && Math.Abs(main.ActualWidth - original.Width) <= 2 && Math.Abs(main.ActualHeight - original.Height) <= 2,
                "Restore does not recover the previous normal bounds");
            Require(System.Windows.Shell.WindowChrome.GetWindowChrome(main).ResizeBorderThickness.Left > 0,
                "The restored border cannot be dragged to resize");
        }
        finally { Set(main, "_forceClose", true); main.Close(); Pump(30); SynchronizationContext.SetSynchronizationContext(context); }
    }

    static void CaptionFitsTop(MainWindow main)
    {
        var root = Find<FrameworkElement>(main, "LayoutRoot");
        foreach (string name in new[] { "BtnMin", "BtnMaximize", "BtnClose" })
        {
            var button = Find<Button>(main, name);
            button.ApplyTemplate();
            var face = (Border)button.Template.FindName("bd", button);
            double top = face.TranslatePoint(new Point(), root).Y;
            Require(Math.Abs(top) < .5 && Math.Abs(face.ActualHeight - 32) < .5,
                $"{name} leaves a gap above its caption face: top={top}, height={face.ActualHeight}");
        }
    }

    public static void Layouts()
    {
        var context = SynchronizationContext.Current;
        var main = new MainWindow();
        var root = (Grid)main.Content;
        var theme = ThemeManager.Mode;
        var language = Loc.Current;
        main.Content = null;
        var host = new Window { Title = "Adaptive layout regression", Content = root, Width = 1120, Height = 730,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.CanMinimize, ShowActivated = false, UseLayoutRounding = true };
        host.SetResourceReference(Control.BackgroundProperty, "CanvasBrush");
        try
        {
            Find<FrameworkElement>(main, "SplashPage").Visibility = Visibility.Collapsed;
            var threads = new List<CodexService.RunningThread> { new("adaptive-fixture", "Layout fixture conversation", false) };
            Set(main, "_threads", threads);
            Find<ComboBox>(main, "ConvoPick").ItemsSource = threads;
            Find<FrameworkElement>(main, "ConvoRow").Visibility = Visibility.Visible;
            Set(main, "_snap", new QuotaSnapshot(QuotaState.Ready, null, "Adaptive layout fixture", UsedPercent: 0, ObservedAt: DateTime.Now));
            host.Show();
            foreach (var lang in new[] { Loc.Lang.Zh, Loc.Lang.En })
            {
                Loc.Set(lang);
                ThemeManager.SetMode(lang == Loc.Lang.Zh ? CodexContinue.ThemeMode.Light : CodexContinue.ThemeMode.Dark, Application.Current);
                Invoke(main, "ApplyLoc");
                Invoke(main, "Render");
                foreach (var (width, height) in new[] { (640, 480), (800, 1200), (960, 540), (1120, 730), (1600, 900), (1920, 800) })
                {
                    root.Width = host.Width = width; root.Height = host.Height = height;
                    Select(main, "Auto"); Pump(480);
                    var page = Find<Grid>(main, "RunPage");
                    Require(Math.Abs(root.ActualWidth - width) < 1 && Math.Abs(root.ActualHeight - height) < 1, "The requested render size was constrained by the physical display");
                    var scroll = Find<ScrollViewer>(main, "RunScroll");
                    Require(page.ActualWidth <= 1441 && page.ActualWidth <= root.ActualWidth + 1,
                        "The home content stretches beyond its readable width or window");
                    var quotaLabel = Find<FrameworkElement>(main, "QuotaLabel");
                    var detected = Find<FrameworkElement>(main, "LblDetect");
                    Require(RectOf(quotaLabel, page).Right + 8 <= RectOf(detected, page).Left,
                        "The full quota readout overlaps the detection time");
                    foreach (string name in new[] { "BoxPrompt", "ConvoPick", "BtnAttach", "BtnRun" }) Inside(Find<FrameworkElement>(main, name), page);
                    var status = RectOf(Find<FrameworkElement>(main, "StatusCard"), page);
                    var command = RectOf(Find<FrameworkElement>(main, "CommandCard"), page);
                    Require(width < 960 ? status.Top >= command.Bottom + 8 : Math.Abs(status.Top - command.Top) < 1 && status.Right <= command.Left,
                        "The status and command cards overlap or fail to reflow");
                    Find<Button>(main, "BtnRun").BringIntoView(); Pump(40);
                    Inside(Find<Button>(main, "BtnRun"), scroll);
                    scroll.ScrollToTop(); Pump(30);
                    Capture(host, root, $"adaptive-{width}x{height}-{lang}-home.png");
                    Select(main, "Manual"); Pump(260);
                    Inside(Find<FrameworkElement>(main, "SendTimePanel"), page);
                    Require(Find<Grid>(main, "ContentHost").Opacity == 1, "Manual switching fades the shared page after a resize");
                    Select(main, "Settings"); Pump(480);
                    var settings = Find<FrameworkElement>(main, "SettingsContent");
                    if (lang == Loc.Lang.En && width == 640)
                        foreach (string name in new[] { "TabManual", "ThemeSystem", "LangSystem" })
                        {
                            var control = Find<Control>(main, name);
                            Console.WriteLine($"Compact English {name}: actual={control.RenderSize}, desired={control.DesiredSize}, content={((ContentControl)control).Content}");
                        }
                    foreach (string name in new[] { "ThemeSystem", "LangSystem", "Idle1800", "Running300", "BtnCheckUpdates" }) Inside(Find<FrameworkElement>(main, name), settings);
                    foreach (var option in Find<Panel>(main, "AccentOptions").Children.OfType<RadioButton>()) Inside(option, settings);
                    foreach (string name in new[] { "LblTheme", "LblAccent", "LblLang" })
                    {
                        var row = (Grid)Find<FrameworkElement>(main, name).Parent;
                        var choices = RectOf((FrameworkElement)row.Children[1], row);
                        Require(choices.Top >= 10 && row.ActualHeight - choices.Bottom >= 10,
                            $"{name} options touch a row boundary at {width}x{height}: top={choices.Top}, bottom={row.ActualHeight - choices.Bottom}");
                    }
                    var idle = RectOf(Find<FrameworkElement>(main, "IdleFrequencyOptions"), settings);
                    var label = RectOf(Find<FrameworkElement>(main, "LblIdleFrequency"), settings);
                    Require(label.Bottom <= idle.Top + 1 || label.Right <= idle.Left + 1, "A frequency label overlaps its options");
                    var settingsScroll = Find<ScrollViewer>(main, "SettingsScroll");
                    Find<Button>(main, "BtnCheckUpdates").BringIntoView(); Pump(40);
                    Inside(Find<Button>(main, "BtnCheckUpdates"), settingsScroll);
                    settingsScroll.ScrollToTop(); Pump(30);
                    Capture(host, root, $"adaptive-{width}x{height}-{lang}-settings.png");
                    Invoke(main, "ShowUpdateDetails", UpdateRelease.Preview); Pump(280);
                    Inside(Find<Button>(main, "BtnDownloadUpdate"), root);
                    Inside(Find<FrameworkElement>(main, "UpdateNotes"), root);
                    Inside(Find<FrameworkElement>(main, "UpdateReleaseDate"), root);
                    Require(Find<FrameworkElement>(main, "UpdateContent").ActualWidth <= 1281, "Release notes stretch across the full large window");
                    Find<Button>(main, "BtnUpdateBack").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); Pump(240);
                }
            }
            root.Width = host.Width = 1120; root.Height = host.Height = 730; Select(main, "Auto"); Pump(480);
            foreach (double scale in new[] { 1d, 1.25, 1.5, 2 }) Capture(host, root, $"adaptive-density-{scale:0.00}.png", scale);
        }
        finally
        {
            host.Content = null; host.Close(); main.Content = root;
            Set(main, "_forceClose", true); main.Close();
            ThemeManager.SetMode(theme, Application.Current); Loc.Set(language);
            SynchronizationContext.SetSynchronizationContext(context);
        }
    }

    static void Select(MainWindow main, string value) => Invoke(main, "SwitchView", Enum.Parse(typeof(MainWindow).GetNestedType("ViewMode", BindingFlags.NonPublic)!, value));
    static T Find<T>(MainWindow main, string name) where T : class => main.FindName(name) as T ?? throw new Exception("Missing adaptive control: " + name);
    static void Invoke(MainWindow main, string method, params object[] args) => typeof(MainWindow).GetMethod(method, Private)!.Invoke(main, args);
    static void Set(MainWindow main, string name, object value) => typeof(MainWindow).GetField(name, Private)!.SetValue(main, value);
    static Rect RectOf(FrameworkElement element, Visual root) => new(element.TranslatePoint(new Point(), (UIElement)root), element.RenderSize);
    static void Inside(FrameworkElement element, FrameworkElement root)
    {
        var bounds = RectOf(element, root);
        Require(bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= root.ActualWidth + 1 && bounds.Bottom <= root.ActualHeight + 1,
            $"{element.Name} is clipped: {bounds} in {root.Name} {root.ActualWidth}x{root.ActualHeight}");
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    static void Capture(Window host, FrameworkElement root, string name, double scale = 1)
    {
        // Detach for export so a test window larger than the physical display is not clipped by its HWND.
        host.Content = null;
        host.UpdateLayout();
        Require(VisualTreeHelper.GetParent(root) == null, "The export surface is still attached to the native display");
        root.InvalidateMeasure();
        root.InvalidateArrange();
        root.Measure(new Size(root.Width, root.Height));
        root.Arrange(new Rect(0, 0, root.Width, root.Height));
        root.UpdateLayout();
        try
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                var bounds = new Rect(0, 0, root.ActualWidth, root.ActualHeight);
                context.DrawRectangle((Brush)Application.Current.FindResource("CanvasBrush"), null, bounds);
                context.DrawRectangle(new VisualBrush(root) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds }, null, bounds);
            }
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * scale), (int)Math.Ceiling(root.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string folder = Path.Combine(AppContext.BaseDirectory, "Artifacts"); Directory.CreateDirectory(folder);
            using var file = File.Create(Path.Combine(folder, name)); encoder.Save(file);
        }
        finally { host.Content = root; }
    }
    [StructLayout(LayoutKind.Sequential)] struct RectI { public int Left, Top, Right, Bottom; public override string ToString() => $"{Left},{Top}-{Right},{Bottom}"; }
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public RectI Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RectI rect);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RectI rect, int size);
    [DllImport("user32.dll")] static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool AreDpiAwarenessContextsEqual(IntPtr first, IntPtr second);
}
