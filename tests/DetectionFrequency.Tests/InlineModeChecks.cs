using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexContinue;

static class InlineModeChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Navigation(MainWindow main) => WithSurface(main, host =>
    {
        var content = Find<Grid>(main, "ContentHost");
        var run = Find<Grid>(main, "RunPage");
        var settings = Find<Grid>(main, "SettingsPage");
        var log = Find<FrameworkElement>(main, "LogPage");
        Require(settings.Parent == content && log.Parent == content && content.IsAncestorOf(run),
            "Settings and log must share the content area below the persistent mode bar");
        Require(main.FindName("LogScrim") == null && main.FindName("BtnBack") == null,
            "The old overlay or back navigation is still present");
        var originalTheme = ThemeManager.Mode;
        var originalLanguage = Loc.Current;
        try
        {
            foreach (var theme in new[] { CodexContinue.ThemeMode.Light, CodexContinue.ThemeMode.Dark })
            {
                ThemeManager.SetMode(theme, Application.Current);
                foreach (var mode in new[] { "Auto", "Settings", "Log", "Manual" })
                {
                    Select(main, mode);
                    Pump(650);
                    CheckSelection(main, mode);
                    var page = mode == "Settings" ? settings : mode == "Log" ? log : run;
                    Require(page.Visibility == Visibility.Visible && content.Opacity == 1,
                        "The selected mode did not settle as visible content");
                    double bottom = Find<Grid>(main, "TabRow").TranslatePoint(new Point(0, 54), host).Y;
                    double top = page.TranslatePoint(new Point(0, 0), host).Y;
                    Require(top >= bottom - 1, "Mode content covers the title or mode bar");
                    Require(page.ActualHeight >= 590 && page.ActualWidth >= 1080,
                        "Inline content does not use the full space below the mode bar");
                    Require(page.RenderTransform.Value.IsIdentity && content.RenderTransform.Value.IsIdentity,
                        "Mode navigation still translates or scales the content");
                    Capture((FrameworkElement)host.Content, $"inline-{theme}-{mode}.png");
                }
            }
            Loc.Set(Loc.Lang.En);
            Invoke(main, "ApplyLoc");
            Select(main, "Settings");
            Pump(650);
            Require(Find<RadioButton>(main, "BtnSettings").ToolTip?.ToString() == Loc.T("settings.title") &&
                Find<RadioButton>(main, "BtnLog").ToolTip?.ToString() == Loc.T("log.title"),
                "The mode buttons do not follow the selected language");
            Capture((FrameworkElement)host.Content, "inline-Dark-Settings-English.png");
        }
        finally
        {
            ThemeManager.SetMode(originalTheme, Application.Current);
            Loc.Set(originalLanguage);
            Invoke(main, "ApplyLoc");
            Select(main, "Auto");
            Pump(550);
        }
    });

    public static void Transitions(MainWindow main) => WithSurface(main, host =>
    {
        var content = Find<Grid>(main, "ContentHost");
        var run = Find<Grid>(main, "RunPage");
        Select(main, "Manual");
        Pump(550);
        Select(main, "Auto");
        Pump(550);
        var picker = Find<ComboBox>(main, "ConvoPick");
        picker.ItemsSource = new[] { new CodexService.RunningThread("inline-test", "Inline test conversation", false) };
        picker.SelectedIndex = 0;
        var prompt = Find<TextBox>(main, "BoxPrompt");
        prompt.Text = "/Continue inline test";
        SetField(main, "_conn", null);
        Invoke(main, "Arm");
        var timer = Field<DispatcherTimer>(main, "_scanTimer");
        timer.Stop();
        var interval = timer.Interval;
        var gear = Find<System.Windows.Shapes.Path>(main, "IconGear");
        string restShape = gear.Data.ToString();
        Find<RadioButton>(main, "BtnSettings").RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0)
            { RoutedEvent = UIElement.MouseEnterEvent });
        Pump(80);
        Require(gear.Data.ToString() == restShape, "Hover alone morphs the inactive settings icon");
        Select(main, "Settings");
        if (SystemParameters.ClientAreaAnimation)
        {
            Pump(65);
            Require(content.Opacity > 0 && content.Opacity < 1 && run.Visibility == Visibility.Visible,
                "The outgoing content snaps away instead of fading");
            Capture((FrameworkElement)host.Content, "inline-fade-out.png");
            Pump(140);
            Require(run.Visibility == Visibility.Collapsed && content.Opacity > 0 && content.Opacity < 1,
                "The new view should fade in after the old one disappears");
            Capture((FrameworkElement)host.Content, "inline-fade-in.png");
        }
        Pump(650);
        Require(gear.Data.ToString() != restShape, "The settings icon does not morph when selected");
        string selectedShape = gear.Data.ToString();
        Find<RadioButton>(main, "BtnSettings").RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0)
            { RoutedEvent = UIElement.MouseLeaveEvent });
        Pump(80);
        Require(gear.Data.ToString() == selectedShape, "The selected icon resets on mouse leave");
        Select(main, "Log");
        Pump(550);
        var list = Find<StackPanel>(main, "LogList");
        int previousCount = list.Children.Count;
        Invoke(main, "AddLogEntry", "inline fixture update", Array.Empty<string>());
        Require(list.Children.Count == previousCount + 1 && Find<TextBlock>(main, "LogEmptyHint").Visibility == Visibility.Collapsed,
            "The inline log does not receive updates while visible");
        Require(Field<bool>(main, "_armed") && timer.Interval == interval && !prompt.IsEnabled,
            "Inspecting settings/log stopped or changed the armed continuation");
        Select(main, "Auto");
        Pump(550);
        Require(prompt.Text == "/Continue inline test" && picker.SelectedIndex == 0 && Field<bool>(main, "_armed"),
            "Returning to the run mode loses the draft, target or armed state");
        Invoke(main, "Disarm", "inline fixture stopped");
        timer.Stop();
        Select(main, "Settings");
        Pump(45);
        double before = content.Opacity;
        Select(main, "Log");
        Require(Math.Abs(content.Opacity - before) < 0.03, "An interrupted fade jumps to full opacity");
        Pump(200);
        Select(main, "Manual");
        Pump(70);
        Select(main, "Settings");
        Pump(650);
        CheckSelection(main, "Settings");
        Require(Find<Grid>(main, "SettingsPage").Visibility == Visibility.Visible &&
            Find<FrameworkElement>(main, "LogPage").Visibility == Visibility.Collapsed && run.Visibility == Visibility.Collapsed &&
            content.Opacity == 1 && content.IsHitTestVisible,
            "A stale animation completion restored an older view or left content inert");
        Select(main, "Manual");
        Pump(550);
        Require(Find<FrameworkElement>(main, "SendTimePanel").Visibility == Visibility.Visible,
            "The manual mode did not restore its send-time controls");
        Select(main, "Auto");
        Pump(550);
        Require(Find<FrameworkElement>(main, "SendTimePanel").Visibility == Visibility.Collapsed,
            "The automatic mode retained manual send-time controls");
    });

    public static void RunModes(MainWindow main) => WithSurface(main, host =>
    {
        Select(main, "Auto");
        Pump(550);
        var content = Find<Grid>(main, "ContentHost");
        var time = Find<FrameworkElement>(main, "SendTimePanel");
        var loop = Find<FrameworkElement>(main, "LoopPanel");
        var shared = new[] { "BoxPrompt", "BtnAttach", "ConvoPick", "BtnRun", "QuotaPct", "TitleStatus" }
            .Select(n => Find<FrameworkElement>(main, n)).ToArray();
        var positions = shared.Select(e => e.TranslatePoint(new Point(), content)).ToArray();
        foreach (string target in new[] { "Manual", "Auto" })
        {
            Select(main, target);
            Pump(65);
            Require(content.Opacity == 1 && content.IsHitTestVisible && Find<Grid>(main, "RunPage").IsVisible,
                "Switching the two run modes fades or disables the shared page");
            for (int i = 0; i < shared.Length; i++)
                Require(shared[i].Opacity == 1 && (shared[i].TranslatePoint(new Point(), content) - positions[i]).Length < .1,
                    "Switching run modes changes shared content opacity or position: " + shared[i].Name);
            if (SystemParameters.ClientAreaAnimation)
                Require(time.IsVisible && loop.IsVisible && time.Opacity is > 0 and < 1 && loop.Opacity is > 0 and < 1,
                    "The send-time and loop controls do not enter and exit with a local fade");
            Capture((FrameworkElement)host.Content, $"run-local-to-{target}.png");
            Pump(400);
            Require((time.Visibility == Visibility.Visible) == (target == "Manual") &&
                (loop.Visibility == Visibility.Visible) == (target == "Auto"), "Run-mode controls settled in the wrong mode");
        }
        Select(main, "Manual");
        Pump(65);
        double previousTime = time.Opacity, previousLoop = loop.Opacity;
        Select(main, "Auto");
        Require(!SystemParameters.ClientAreaAnimation || (Math.Abs(time.Opacity - previousTime) < .03 && Math.Abs(loop.Opacity - previousLoop) < .03),
            "Reversing a local transition resets its opacity");
        Pump(45);
        Select(main, "Manual");
        Pump(400);
        Require(time.IsVisible && time.Opacity == 1 && loop.Visibility == Visibility.Collapsed && content.Opacity == 1,
            "A stale local exit hid the active controls");
        Select(main, "Auto");
        Pump(50);
        Select(main, "Settings");
        Pump(550);
        Require(Find<Grid>(main, "SettingsPage").IsVisible && content.Opacity == 1,
            "A local transition broke navigation to Settings");
        Select(main, "Manual");
        Pump(40);
        Select(main, "Auto");
        Pump(550);
        Require(Find<Grid>(main, "RunPage").IsVisible && loop.IsVisible && time.Visibility == Visibility.Collapsed && content.Opacity == 1,
            "Interrupting a page transition with a run-mode switch left the wrong content");
        foreach (string target in new[] { "Manual", "Auto" })
        {
            Select(main, target);
            Pump(35);
            Invoke(main, "SetCommandLocked", true);
            Pump(300);
            Require((time.Visibility == Visibility.Visible) == (target == "Manual") &&
                (loop.Visibility == Visibility.Visible) == (target == "Auto"), "Running-state dimming interrupted a mode entrance or exit");
            Require(!Find<FrameworkElement>(main, "SendTimeContent").IsEnabled,
                "The manual time editor is not locked with the other command inputs");
            Invoke(main, "SetCommandLocked", false);
            Pump(200);
        }
    });

    static void CheckSelection(MainWindow main, string mode)
    {
        var buttons = new[] { "TabAuto", "TabManual", "BtnLog", "BtnSettings" }.Select(n => Find<RadioButton>(main, n)).ToArray();
        Require(buttons.Count(b => b.IsChecked == true) == 1, "More than one navigation mode is selected");
        var chosen = buttons.Single(b => b.IsChecked == true);
        Require((string)chosen.Tag == mode, "The selected navigation card does not match the visible mode");
        var face = Find<Border>(main, "ModePill");
        var expected = ((SolidColorBrush)Application.Current.FindResource("ModeSelectedBrush")).Color;
        Require(face.Opacity == 1 && ((SolidColorBrush)face.Background).Color == expected &&
            ((SolidColorBrush)chosen.Foreground).Color == ((SolidColorBrush)Application.Current.FindResource("AccentTextBrush")).Color,
            "The active mode is not a blue card with white ink");
        double x = chosen.TranslatePoint(new Point(), Find<Grid>(main, "TabRow")).X;
        Require(Math.Abs(((TranslateTransform)face.RenderTransform).X - x) < 0.1 &&
            Math.Abs(face.Width - chosen.ActualWidth) < 0.1,
            "The shared indicator does not align with the selected mode");
    }

    public static void MenuHover(MainWindow main) => WithSurface(main, host =>
    {
        var button = Find<Button>(main, "BtnGithub");
        var log = Find<RadioButton>(main, "BtnLog");
        var row = Find<Grid>(main, "TabRow");
        Require(Equals(button.Tag, "https://github.com/Belate-stay/Codex-Continue/"), "The GitHub button has a different repository destination");
        Require(button.TranslatePoint(new Point(button.ActualWidth, 0), row).X + 6 <= log.TranslatePoint(new Point(), row).X,
            "GitHub is not placed to the left of the log button");
        var view = Field<object>(main, "_view");
        foreach (string name in new[] { "TabAuto", "TabManual", "BtnLog", "BtnSettings" })
        {
            var control = Find<System.Windows.Controls.Primitives.ButtonBase>(main, name);
            control.ApplyTemplate();
            Require(control.Cursor == System.Windows.Input.Cursors.Hand, $"{name} does not use the hand cursor");
            Require(!control.Template.Triggers.Cast<TriggerBase>().Any(t => t is Trigger trigger && trigger.Property == UIElement.IsMouseOverProperty ||
                t is MultiTrigger multi && multi.Conditions.Cast<Condition>().Any(c => c.Property == UIElement.IsMouseOverProperty)),
                $"{name} still changes appearance on pointer hover");
            var glyph = control.Template.FindName("GithubGlyph", control) as FrameworkElement;
            var pose = glyph?.RenderTransform.Value;
            control.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0)
                { RoutedEvent = UIElement.MouseEnterEvent });
            Pump(300);
            Require(glyph == null || glyph.RenderTransform.Value == pose, "The GitHub mark still moves on pointer hover");
            Require(Equals(Field<object>(main, "_view"), view), "Hovering a navigation button changed the selected mode");
            control.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0)
                { RoutedEvent = UIElement.MouseLeaveEvent });
        }
        button.ApplyTemplate();
        button.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseLeaveEvent });
        Pump(240);
        var githubGlyph = (FrameworkElement)button.Template.FindName("GithubGlyph", button);
        var transforms = (TransformGroup)githubGlyph.RenderTransform;
        var scale = (ScaleTransform)transforms.Children[0];
        var lift = (TranslateTransform)transforms.Children[2];
        button.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
        Pump(70);
        if (SystemParameters.ClientAreaAnimation)
            Require(lift.Y < 0 && lift.Y > -2 && scale.ScaleX > 1 && scale.ScaleX < 1.08, "GitHub no longer has eased icon feedback");
        button.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseLeaveEvent });
        Pump(260);
        Require(Math.Abs(lift.Y) < .01 && Math.Abs(scale.ScaleX - 1) < .01 && button.Template.FindName("GithubFace", button) == null,
            "GitHub does not settle after hover or restores a tinted hover card");
        Capture((FrameworkElement)host.Content, "navigation-pointer-only.png");
    });

    public static void Slider(MainWindow main) => WithSurface(main, host =>
    {
        var row = Find<Grid>(main, "TabRow");
        var pill = Find<Border>(main, "ModePill");
        var layer = Find<Grid>(main, "ModePillLayer");
        var track = Find<System.Windows.Shapes.Path>(main, "ModeTrackFace");
        Require(track.Fill is SolidColorBrush brush && brush.Color ==
            ((SolidColorBrush)Application.Current.FindResource("PillBrush")).Color,
            "The navigation tracks do not use the themed white surface");
        Require(layer.Clip is GeometryGroup group && group.Children.Count == 2 &&
            group.Children.Cast<RectangleGeometry>().All(r => r.RadiusX == 9 && r.RadiusY == 9),
            "The indicator must be clipped to the two separate rounded tracks");
        var clip = (GeometryGroup)layer.Clip;
        var left = ((RectangleGeometry)clip.Children[0]).Rect;
        var right = ((RectangleGeometry)clip.Children[1]).Rect;
        var gap = new Point((left.Right + right.Left) / 2, left.Top + left.Height / 2);
        Require(!clip.FillContains(gap) && clip.FillContains(new Point(left.Left + 10, gap.Y)) &&
            clip.FillContains(new Point(right.Left + 10, gap.Y)),
            "The disconnected middle space is still part of the indicator's visible track");
        Select(main, "Auto");
        Pump(600);
        double start = X();
        var manual = Find<RadioButton>(main, "TabManual");
        double target = manual.TranslatePoint(new Point(), row).X;
        Select(main, "Manual");
        if (!SystemParameters.ClientAreaAnimation)
        {
            CheckSelection(main, "Manual");
            return;
        }
        double adjacentMs = Field<System.Windows.Media.Animation.DoubleAnimation>(main, "_modeSlide").Duration.TimeSpan.TotalMilliseconds;
        Console.WriteLine($"Slider initial: start={start:0.###}, current={X():0.###}, target={target:0.###}, animationFrom={Field<System.Windows.Media.Animation.DoubleAnimation>(main, "_modeSlide").From:0.###}");
        Require(Math.Abs(X() - start) < 0.1, "The indicator jumps to the destination before it starts sliding");
        Pump(80);
        double first = X();
        Require(first > start && first < target && first - start > (target - start) * 80 / adjacentMs,
            "The indicator is not sliding with a fast initial rate");
        Capture((FrameworkElement)host.Content, "slider-adjacent.png");
        Pump(80);
        double second = X();
        Require(second > first && second - first < first - start,
            "The indicator does not decelerate as it approaches its destination");
        Pump(450);
        CheckSelection(main, "Manual");
        var position = (TranslateTransform)pill.RenderTransform;
        position.X = target + manual.ActualWidth - manual.Padding.Right - pill.Width - 4;
        Invoke(main, "UpdateModeInk");
        Require(((SolidColorBrush)manual.Foreground).Color != Colors.White,
            "A partially covered label turns white on the uncovered white track");
        Invoke(main, "MoveModePill", false, 380);

        start = X();
        Select(main, "Settings");
        double crossMs = Field<System.Windows.Media.Animation.DoubleAnimation>(main, "_modeSlide").Duration.TimeSpan.TotalMilliseconds;
        Require(crossMs > adjacentMs && crossMs <= adjacentMs + 100,
            "Cross-group travel must be only slightly longer than adjacent travel");
        double crossDistance = Find<RadioButton>(main, "BtnSettings").TranslatePoint(new Point(), row).X - start;
        Require(crossDistance / crossMs > (target - left.Left) / adjacentMs * 3,
            "The long cross-group trip moves too slowly");
        Until(() => X() > left.Right && X() + pill.Width < right.Left, 250,
            "Cross-group movement never passes through the hidden middle space");
        Capture((FrameworkElement)host.Content, "slider-hidden-middle.png");
        Require(((SolidColorBrush)Find<RadioButton>(main, "BtnSettings").Foreground).Color != Colors.White,
            "The destination icon turns white before the blue indicator arrives");
        Pump(550);
        CheckSelection(main, "Settings");
        Require(Find<Grid>(main, "SettingsPage").Visibility == Visibility.Visible &&
            Find<Grid>(main, "ContentHost").Opacity == 1,
            "The content and cross-group indicator do not settle together");

        Select(main, "Auto");
        Until(() => X() > left.Right && X() + pill.Width < right.Left, 250,
            "The return trip does not hide the indicator between the groups");
        double interruptedX = X(), interruptedWidth = pill.Width;
        Select(main, "Log");
        Require(Math.Abs(X() - interruptedX) < 0.1 && Math.Abs(pill.Width - interruptedWidth) < 0.1,
            "Reversing direction resets the indicator's position or width");
        Pump(650);
        CheckSelection(main, "Log");
        Require(Find<FrameworkElement>(main, "LogPage").Visibility == Visibility.Visible &&
            Find<Grid>(main, "SettingsPage").Visibility == Visibility.Collapsed,
            "An interrupted indicator restores an outdated content mode");
        Require(Field<object?>(main, "_modeSlide") == null,
            "The mode animation does not release its render callback after settling");
        Select(main, "Auto");
        Pump(600);
        double X() => ((TranslateTransform)pill.RenderTransform).X;
    });

    static void Select(MainWindow main, string mode) => Find<RadioButton>(main, mode switch
    {
        "Auto" => "TabAuto", "Manual" => "TabManual", "Log" => "BtnLog", _ => "BtnSettings"
    }).IsChecked = true;

    static void WithSurface(MainWindow main, Action<Window> action)
    {
        var root = (Grid)main.Content;
        var splash = Find<Grid>(main, "SplashPage");
        var original = splash.Visibility;
        splash.Visibility = Visibility.Collapsed;
        main.Content = null;
        var host = new Window { Title = "Inline mode regression", Width = 1120, Height = 730, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.CanMinimize,
            ShowActivated = false, UseLayoutRounding = true, SnapsToDevicePixels = true, Content = root,
            Background = (Brush)Application.Current.FindResource("CanvasBrush") };
        try { host.Show(); Pump(80); action(host); }
        finally { host.Content = null; host.Close(); main.Content = root; splash.Visibility = original; }
    }

    static void Capture(FrameworkElement view, string name)
    {
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            var bounds = new Rect(0, 0, view.ActualWidth, view.ActualHeight);
            drawing.DrawRectangle((Brush)Application.Current.FindResource("CanvasBrush"), null, bounds);
            drawing.DrawRectangle(new VisualBrush(view), null, bounds);
        }
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Artifacts"));
        using var file = File.Create(Path.Combine(AppContext.BaseDirectory, "Artifacts", name));
        encoder.Save(file);
    }
    static T Find<T>(MainWindow main, string name) where T : class => main.FindName(name) as T
        ?? throw new Exception("Missing inline mode control: " + name);
    static object? Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private)!.Invoke(target, args);
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private)!.GetValue(target)!;
    static void SetField(object target, string name, object? value) => target.GetType().GetField(name, Private)!.SetValue(target, value);
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Until(Func<bool> condition, int ms, string message)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && watch.ElapsedMilliseconds < ms) Pump(8);
        Require(condition(), message);
    }
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
