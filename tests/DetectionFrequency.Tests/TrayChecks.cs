using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexContinue;

static class TrayChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Menu()
    {
        var context = SynchronizationContext.Current;
        var window = new MainWindow { ShowActivated = false };
        UpdateChecks.IsolateLoadedWindow(window);
        var language = Loc.Current;
        var theme = ThemeManager.Mode;
        var accent = ThemeManager.Accent;
        try
        {
            SetField(window, "_scanTask", Task.CompletedTask);
            window.Show(); Pump(100);
            var tray = Field<object>(window, "_tray");
            Require(Field<object>(tray, "_menu") is ContextMenu, "The tray still uses the square Windows Forms menu");
            var menu = Field<ContextMenu>(tray, "_menu");
            var open = Field<MenuItem>(tray, "_open");
            var settings = Field<MenuItem>(tray, "_settings");
            var run = Field<MenuItem>(tray, "_run");
            var exit = Field<MenuItem>(tray, "_exit");
            Invoke(window, "RestoreFromTray"); Pump(80);
            Require(menu.Items.Cast<object>().SequenceEqual(new[] { open, settings, run, exit }), "The four tray commands are out of order or contain extra rows");
            Require(!run.IsEnabled && Equals(run.ToolTip, Loc.T("cmd.incomplete")), "An incomplete command can run from the tray");
            var symbol = (System.Windows.Shapes.Path)run.Icon;
            string triangle = symbol.Data.ToString();
            foreach (var appearance in new[] { CodexContinue.ThemeMode.Light, CodexContinue.ThemeMode.Dark })
            foreach (var lang in new[] { Loc.Lang.Zh, Loc.Lang.En })
            {
                ThemeManager.SetMode(appearance, Application.Current);
                ThemeManager.SetAccent("black", Application.Current, animate: false);
                Loc.Set(lang); Invoke(window, "ApplyLoc");
                Invoke(tray, "UpdateState", false, true);
                Require(Text(open) == Loc.T("tray.open") && Text(settings) == Loc.T("settings.title") &&
                    Text(run) == Loc.T("cmd.run") && Text(exit) == Loc.T("tray.exit"), "A tray command is not localized");
                Invoke(window, "RestoreFromTray"); Pump(80);
                Invoke(tray, "ShowMenu"); Pump(200);
                Require(menu.IsOpen && PresentationSource.FromVisual(menu) != null,
                    $"The tray popup lost its native presentation source before keyboard input: appearance={appearance}, language={lang}, open={menu.IsOpen}, visible={menu.IsVisible}, size={menu.ActualWidth}/{menu.ActualHeight}, source={PresentationSource.FromVisual(menu)}, ownerVisible={window.IsVisible}, ownerActive={window.IsActive}, focus={Keyboard.FocusedElement}");
                var card = (Border)menu.Template.FindName("TrayMenuCard", menu);
                Require(card.CornerRadius.TopLeft >= 10 && menu.FontSize >= 12.5 && menu.FontSize < 14 &&
                    menu.ActualWidth >= 220 && menu.ActualWidth < 244,
                    "The compact menu is square, oversized or uses unreadable type");
                Require(card.BorderBrush is SolidColorBrush { Color.A: 0 }, "The tray card still has a visible solid outline");
                double previous = -1;
                foreach (MenuItem item in menu.Items)
                {
                    var top = item.TranslatePoint(new Point(), menu).Y;
                    Require(item.ActualHeight >= 35 && item.ActualHeight < 40 && (previous < 0 || top - previous >= 35),
                        "Compact tray rows are oversized, too tight or overlap");
                    previous = top;
                }
                Require(((SolidColorBrush)symbol.Fill).Color == ((SolidColorBrush)Application.Current.FindResource("ErrBrush")).Color,
                    $"The run symbol is stale in {appearance}/{lang}: actual={((SolidColorBrush)symbol.Fill).Color}, expected={((SolidColorBrush)Application.Current.FindResource("ErrBrush")).Color}");
                Require(((SolidColorBrush)card.Background).Color == ((SolidColorBrush)Application.Current.FindResource("PillBrush")).Color,
                    "The detached menu card does not follow the current appearance");
                MenuKey(menu, Key.Down); Pump(35);
                var firstHover = (Border)open.Template.FindName("TrayItemHover", open);
                Require(open.IsHighlighted && (!SystemParameters.ClientAreaAnimation || firstHover.Opacity > 0 && firstHover.Opacity < 1),
                    $"Keyboard selection has no gradual highlight feedback: highlighted={open.IsHighlighted}, opacity={firstHover.Opacity}, focused={Keyboard.FocusedElement}");
                Pump(150);
                Require(firstHover.Opacity > .99, "The highlight does not settle on the selected row");
                Capture(menu, $"tray-{appearance}-{lang}-hover.png");
                MenuKey(menu, Key.Down); Pump(35);
                var secondHover = (Border)settings.Template.FindName("TrayItemHover", settings);
                Require(settings.IsHighlighted && (!SystemParameters.ClientAreaAnimation || firstHover.Opacity > 0 && firstHover.Opacity < 1 && secondHover.Opacity > 0),
                    "Moving selection removes the old highlight abruptly");
                Pump(180);
                Require(firstHover.Opacity < .01 && secondHover.Opacity > .99, "Interrupted menu selection leaves a stale highlight");
                Capture(menu, $"tray-{appearance}-{lang}-run.png");
                Invoke(tray, "UpdateState", true, true);
                Require(Text(run) == Loc.T("cmd.stop") && symbol.Data.ToString() != triangle && run.IsEnabled,
                    "The open menu does not update to a stop square");
                Capture(menu, $"tray-{appearance}-{lang}-stop.png");
                MenuKey(menu, Key.Escape);
                Pump(50);
                Require(!menu.IsOpen, "Escape does not dismiss the tray menu");
            }
            Invoke(window, "UpdateCommandState");
            PerformClick(run);
            Require(!Field<bool>(window, "_armed"), "Disabled tray Run armed a task");
            window.Hide(); PerformClick(settings); Pump(480);
            Require(window.IsVisible && ((Grid)window.FindName("SettingsPage")).Visibility == Visibility.Visible &&
                ((Grid)window.FindName("SplashPage")).Visibility == Visibility.Collapsed &&
                ((Grid)window.FindName("ConnectPage")).Visibility == Visibility.Collapsed,
                "Tray Settings does not open directly from the startup screen");
            var prompt = (TextBox)window.FindName("BoxPrompt");
            var picker = (ComboBox)window.FindName("ConvoPick");
            picker.ItemsSource = new[] { new CodexService.RunningThread("tray-menu-fixture", "Tray menu fixture", false) };
            picker.SelectedIndex = 0; prompt.Text = "";
            Require(!run.IsEnabled, "Tray Run accepts a missing prompt");
            prompt.Text = "/Continue tray menu fixture";
            Require(run.IsEnabled, "Tray Run does not enable for a complete command");
            SetField(window, "_mode", Enum.Parse(typeof(MainWindow).GetNestedType("RunMode", BindingFlags.NonPublic)!, "Manual"));
            SetField(window, "_manualSendAt", DateTime.Now.AddHours(3));
            SetField(window, "_sendTimeTouched", true);
            ((TextBox)window.FindName("BoxSendTime")).Text = DateTime.Now.AddHours(3).ToString("HH:mm");
            SetField(window, "_scanTask", Task.CompletedTask);
            Invoke(window, "Arm");
            var deadline = Field<DateTime?>(window, "_manualSendAt");
            Invoke(window, "ShowUpdateDetails", UpdateRelease.Preview); Pump(250);
            window.Hide(); PerformClick(settings); Pump(480);
            Require(Field<bool>(window, "_armed") && !Field<bool>(window, "_updateDetailsOpen") &&
                ((Grid)window.FindName("SettingsPage")).Visibility == Visibility.Visible &&
                Field<DateTime?>(window, "_manualSendAt") == deadline && prompt.Text == "/Continue tray menu fixture",
                "Tray Settings interrupts the active task or remains behind release details");
            window.Hide(); PerformClick(run);
            Require(!Field<bool>(window, "_armed") && !window.IsVisible && Text(run) == Loc.T("cmd.run") && symbol.Data.ToString() == triangle,
                "Tray Stop loses its state, opens the window or does not stop the hidden task");

            int restored = 0, configured = 0, toggled = 0, exited = 0;
            using var callbacks = (IDisposable)Activator.CreateInstance(tray.GetType(), (Action)(() => restored++),
                (Action)(() => configured++), (Action)(() => toggled++), (Action)(() => exited++))!;
            Invoke(callbacks, "UpdateState", false, true);
            foreach (string name in new[] { "_open", "_settings", "_run", "_exit" }) PerformClick(Field<MenuItem>(callbacks, name));
            Require(restored == 1 && configured == 1 && toggled == 1 && exited == 1, "A tray command invokes the wrong callback or runs twice");
        }
        finally
        {
            SetField(window, "_forceClose", true); window.Close();
            Loc.Set(language); ThemeManager.SetMode(theme, Application.Current); ThemeManager.SetAccent(accent, Application.Current, animate: false);
            SynchronizationContext.SetSynchronizationContext(context);
        }
    }

    static void MenuKey(ContextMenu menu, Key key) => (Keyboard.FocusedElement is UIElement focused && menu.IsAncestorOf(focused) ? focused : menu).RaiseEvent(
        new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(menu), Environment.TickCount, key)
            { RoutedEvent = Keyboard.KeyDownEvent });

    static void Capture(FrameworkElement menu, string name)
    {
        menu.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth * 2), (int)Math.Ceiling(menu.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(menu);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string folder = Path.Combine(AppContext.BaseDirectory, "Artifacts"); Directory.CreateDirectory(folder);
        using var file = File.Create(Path.Combine(folder, name)); encoder.Save(file);
    }

    public static void Lifecycle()
    {
        var window = new MainWindow { ShowActivated = false };
        UpdateChecks.IsolateLoadedWindow(window);
        var language = Loc.Current;
        bool closed = false;
        window.Closed += (_, _) => closed = true;
        try
        {
            // Coalesce detection in the fixture; no external probe or continuation is sent.
            SetField(window, "_scanTask", Task.CompletedTask);
            window.Show();
            Pump(70);
            Require(Field<bool>(window, "_started"), "First-load initialization did not complete");
            var titleIcon = (Image)window.FindName("TitleIcon");
            titleIcon.Visibility = Visibility.Visible;
            ((Grid)window.FindName("SplashPage")).Visibility = Visibility.Collapsed;
            var tray = Field<object>(window, "_tray");
            var icon = Field<object>(tray, "_icon");
            Require(Visible(icon), "The notification icon is missing while the app is open");
            var prompt = (TextBox)window.FindName("BoxPrompt");
            var picker = (ComboBox)window.FindName("ConvoPick");
            prompt.Text = "/Continue tray test";
            picker.ItemsSource = new[] { new CodexService.RunningThread("tray-test", "Tray test conversation", false) };
            picker.SelectedIndex = 0;
            SetField(window, "_scanTask", Task.CompletedTask);
            var runMode = typeof(MainWindow).GetNestedType("RunMode", BindingFlags.NonPublic)!;
            SetField(window, "_mode", Enum.Parse(runMode, "Manual"));
            SetField(window, "_manualSendAt", DateTime.Now.AddHours(3));
            SetField(window, "_sendTimeTouched", true);
            ((TextBox)window.FindName("BoxSendTime")).Text = DateTime.Now.AddHours(3).ToString("HH:mm");
            Invoke(window, "Arm");
            var scan = Field<DispatcherTimer>(window, "_scanTimer");
            var manual = Field<DispatcherTimer>(window, "_manualTimer");
            var interval = scan.Interval;
            var deadline = Field<DateTime?>(window, "_manualSendAt");
            window.Close();
            Pump(70);
            Require(!closed && !window.IsVisible && Visible(icon),
                "Closing the window ends the app instead of keeping it in the tray");
            Require(window.IsLoaded, "Hiding unloads the window and prevents background scans from rescheduling");
            Require(Field<bool>(window, "_armed") && scan.IsEnabled && manual.IsEnabled && scan.Interval == interval,
                "Hiding the window stops the armed continuation or its detection/deadline timers");
            Require(prompt.Text == "/Continue tray test" && picker.SelectedIndex == 0 &&
                Field<DateTime?>(window, "_manualSendAt") == deadline,
                "Hiding the window changes its command, conversation or scheduled time");
            Require(window.FindName("CloseDialog") == null, "Ordinary close still opens the old exit confirmation");

            var open = Field<object>(tray, "_open");
            for (int i = 0; i < 3; i++)
            {
                PerformClick(open);
                Pump(50);
                Require(window.IsVisible && window.IsActive && !closed && Field<object>(window, "_tray") == tray && Field<bool>(window, "_armed") &&
                    titleIcon.Visibility == Visibility.Visible,
                    "Restoring resets the run state or recreates the tray icon");
                if (i < 2) { window.Close(); Pump(30); }
            }
            window.WindowState = WindowState.Minimized;
            window.Close();
            PerformClick(open);
            Pump(70);
            Require(window.IsVisible && window.IsActive && window.WindowState == WindowState.Normal,
                "Restoring a minimized window leaves it minimized");

            foreach (var lang in new[] { Loc.Lang.En, Loc.Lang.Zh })
            {
                Loc.Set(lang);
                Invoke(window, "ApplyLoc");
                Require(Text(open) == Loc.T("tray.open") && Text(Field<object>(tray, "_exit")) == Loc.T("tray.exit"),
                    "Tray actions do not follow the selected language");
            }
            window.Close();
            PerformClick(Field<object>(tray, "_exit"));
            Pump(70);
            Require(closed && !Visible(icon) && !scan.IsEnabled && !manual.IsEnabled &&
                !Field<DispatcherTimer>(window, "_connectTimer").IsEnabled,
                "The tray Exit action does not close the app and stop all schedulers");
            var menu = Field<ContextMenu>(tray, "_menu");
            Require(!menu.IsOpen && menu.Items.Count == 0, "Exit leaves the tray context menu open or populated");
        }
        finally
        {
            Loc.Set(language);
            if (!closed)
            {
                SetField(window, "_forceClose", true);
                window.Close();
            }
        }
    }

    public static void Idle()
    {
        var window = new MainWindow { ShowActivated = false };
        UpdateChecks.IsolateLoadedWindow(window);
        bool closed = false;
        window.Closed += (_, _) => closed = true;
        try
        {
            SetField(window, "_started", true);
            window.Show();
            var scan = Field<DispatcherTimer>(window, "_scanTimer");
            SetField(window, "_scanTask", Task.CompletedTask);
            Invoke(window, "UpdateScanInterval");
            var interval = scan.Interval;
            window.Close();
            Require(!closed && !window.IsVisible && scan.IsEnabled && scan.Interval == interval,
                "Idle closing must keep automatic detection running in the tray");
            Invoke(window, "RestoreFromTray");
            Require(window.IsVisible && !Field<bool>(window, "_armed"), "Restoring an idle window starts a continuation");
            Invoke(window, "App_SessionEnding", Application.Current,
                null!);
            window.Close();
            Require(closed && !scan.IsEnabled, "Windows session shutdown is cancelled by close-to-tray behavior");
        }
        finally
        {
            if (!closed) { SetField(window, "_forceClose", true); window.Close(); }
        }
    }

    static bool Visible(object icon) => (bool)icon.GetType().GetProperty("Visible")!.GetValue(icon)!;
    static string Text(object item) => (string)((MenuItem)item).Header;
    static void PerformClick(object item)
    {
        var menu = (MenuItem)item;
        if (menu.IsEnabled) menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }
    static T Field<T>(object obj, string name) => (T)(obj.GetType().GetField(name, Private)
        ?? throw new Exception("Missing tray lifecycle field " + name)).GetValue(obj)!;
    static void SetField(object obj, string name, object? value) => (obj.GetType().GetField(name, Private)
        ?? throw new Exception("Missing tray lifecycle field " + name)).SetValue(obj, value);
    static object? Invoke(object obj, string name, params object[] args) => (obj.GetType().GetMethod(name, Private)
        ?? throw new Exception("Missing tray lifecycle method " + name)).Invoke(obj, args);
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
