using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexContinue;

static class AccentColorChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] Presets = { "system", "blue", "black", "green", "gray", "pink" };

    public static void Settings()
    {
        var property = typeof(AppSettings).GetProperty("Accent") ?? throw new Exception("Missing accent setting");
        Require(Equals(property.GetValue(new AppSettings()), "blue"), "New settings do not default to blue");
        var legacy = JsonSerializer.Deserialize<AppSettings>("{\"Theme\":\"dark\",\"Language\":\"en\"}")!;
        Require(Equals(property.GetValue(legacy), "blue") && legacy.Theme == "dark" && legacy.Language == "en",
            "Legacy settings do not retain their preferences and receive blue by default");
        foreach (string preset in Presets)
        {
            var settings = new AppSettings();
            property.SetValue(settings, preset);
            var loaded = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
            Require(Equals(property.GetValue(loaded), preset), "The accent does not persist: " + preset);
        }
        foreach (var invalid in new string?[] { null, "", "orange", "Blue" })
        {
            property.SetValue(legacy, invalid);
            Require(Equals(property.GetValue(legacy), "blue"), "An invalid accent does not fall back to blue");
        }
    }

    public static void Palette(MainWindow main)
    {
        var originalTheme = ThemeManager.Mode;
        var language = Loc.Current;
        var originalAccent = Accent();
        var settingsPage = (Grid)main.FindName("SettingsPage");
        var parent = (Panel)settingsPage.Parent;
        int index = parent.Children.IndexOf(settingsPage);
        parent.Children.Remove(settingsPage);
        settingsPage.Visibility = Visibility.Visible;
        var host = new Window { Title = "Accent palette regression", Width = 1120, Height = 628,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.CanMinimize, ShowActivated = false, Content = settingsPage };
        try
        {
            var options = main.FindName("AccentOptions") as Panel ?? throw new Exception("Missing accent choices");
            Require(string.Join(",", options.Children.OfType<RadioButton>().Select(r => (string)r.Tag)) == string.Join(",", Presets),
                "The accent row does not expose Follow system and the five presets");
            host.SetResourceReference(Control.BackgroundProperty, "CanvasBrush");
            host.Show();
            foreach (var theme in new[] { CodexContinue.ThemeMode.Light, CodexContinue.ThemeMode.Dark })
            {
                ThemeManager.SetMode(theme, Application.Current);
                Invoke(main, "LoadSettingsSelection");
                var fixedKeys = new[] { "QuotaBlueBrush", "QuotaYellowBrush", "QuotaRedBrush", "QuotaTrailBrush", "OkBrush", "ErrBrush", "LogAccentBrush", "CaptionCloseHoverBrush" };
                Require(Same(Ink("CaptionCloseHoverBrush"), Color.FromRgb(196, 43, 28)), "The Close highlight is not red independently of the accent");
                var fixedColors = fixedKeys.Select(Ink).ToArray();
                Invoke(main, "AddLogEntry", "Palette special-ink sample", new[] { "special-ink" });
                var entry = (TextBlock)((StackPanel)main.FindName("LogList")).Children[^1];
                var logRuns = entry.Inlines.OfType<Run>().ToArray();
                var logInk = logRuns.Select(r => ((SolidColorBrush)r.Foreground).Color).ToArray();
                var quotaInk = ((SolidColorBrush)((TextBlock)main.FindName("QuotaPct")).Foreground).Color;
                var seen = new HashSet<string>();
                foreach (string preset in Presets)
                {
                    SetAccent(preset, false);
                    foreach (RadioButton option in options.Children) option.IsChecked = (string)option.Tag == preset;
                    Pump(180);
                    var selected = options.Children.OfType<RadioButton>().Single(r => r.IsChecked == true);
                    selected.ApplyTemplate();
                    var face = (Border)selected.Template.FindName("checkOverlay", selected);
                    Require(Same(((SolidColorBrush)face.Background).Color, Ink("ModeSelectedBrush")) && face.Opacity == 1,
                        "Selected settings do not use a solid accent card: " + preset);
                    Require(Same(((SolidColorBrush)selected.Foreground).Color, Ink("AccentTextBrush")) &&
                        Contrast(Ink("ModeSelectedBrush"), Ink("AccentTextBrush")) >= 4.5,
                        "Selected-card text lacks contrast: " + theme + "/" + preset);
                    var run = (Button)main.FindName("BtnRun");
                    Require(Same(((SolidColorBrush)run.Background).Color, Ink("AccentBrush")), "The Run key does not follow the preset");
                    Require(Same(((SolidColorBrush)((Button)main.FindName("BtnRecheck")).Background).Color, Ink("ButtonSurfaceBrush")) &&
                        Same(((SolidColorBrush)((Button)main.FindName("BtnAttach")).Foreground).Color, Ink("AccentInkBrush")),
                        "Secondary and attachment buttons do not follow the preset");
                    Require(Same(((SolidColorBrush)((Border)main.FindName("ModePill")).Background).Color, Ink("ModeSelectedBrush")),
                        "The mode slider does not follow the preset");
                    Require(seen.Add(Ink("AccentBrush").ToString()), "Two presets have the same button color");
                    for (int i = 0; i < fixedKeys.Length; i++)
                        Require(Same(Ink(fixedKeys[i]), fixedColors[i]), "Accent change recolors special ink: " + fixedKeys[i]);
                    Require(Same(((SolidColorBrush)((TextBlock)main.FindName("QuotaPct")).Foreground).Color, quotaInk),
                        "The displayed quota text changes with the accent");
                    for (int i = 0; i < logRuns.Length; i++)
                        Require(Same(((SolidColorBrush)logRuns[i].Foreground).Color, logInk[i]), "The displayed log highlights change with the accent");
                    Capture(settingsPage, $"accent-{theme}-{preset}.png");
                }
            }
            SetAccent("blue", false);
            var brush = (SolidColorBrush)Application.Current.FindResource("ModeSelectedBrush");
            Color start = brush.Color;
            SetAccent("green", true);
            Require(Same(brush.Color, start), "Accent color jumps on its first frame");
            Pump(80);
            var middle = brush.Color;
            Require(!SystemParameters.ClientAreaAnimation || !Same(middle, start), "The accent transition does not interpolate");
            SetAccent("pink", true);
            Require(Same(brush.Color, middle), "Interrupting a color transition jumps back to its base color");
            Pump(400);
            var final = brush.Color;
            SetAccent("pink", false);
            Require(Same(final, Ink("ModeSelectedBrush")), "An interrupted transition ends at an outdated color");
            ThemeManager.SetMode(CodexContinue.ThemeMode.Light, Application.Current);
            Require(Accent() == "pink", "Appearance changes reset the accent preset");
            Loc.Set(Loc.Lang.En);
            Invoke(main, "ApplyLoc");
            Require(((TextBlock)main.FindName("LblAccent")).Text == "Accent color", "The accent row is not localized");
            Pump(140);
            ((ScrollViewer)main.FindName("SettingsScroll")).ScrollToBottom(); Pump(80);
            var version = main.FindName("ValVersion") as TextBlock ?? throw new Exception("Missing About version row");
            Require(version.Text == "Version 1.0" && ((TextBlock)main.FindName("LblAboutSettings")).Text == "About",
                "About does not display the localized category and current Beta version");
            Require(version.TranslatePoint(new Point(0, version.ActualHeight), settingsPage).Y <= settingsPage.ActualHeight - 16,
                "The About section pushes settings outside the content viewport");
            Capture(settingsPage, "accent-Light-pink-English.png");
        }
        finally
        {
            host.Content = null;
            host.Close();
            parent.Children.Insert(index, settingsPage);
            settingsPage.Visibility = Visibility.Collapsed;
            ThemeManager.SetMode(originalTheme, Application.Current);
            SetAccent(originalAccent, false);
            Loc.Set(language);
            Invoke(main, "ApplyLoc");
        }
    }

    static string Accent() => (string)(typeof(ThemeManager).GetProperty("Accent")
        ?? throw new Exception("Missing current accent palette")).GetValue(null)!;
    static void SetAccent(string name, bool animate) => (typeof(ThemeManager).GetMethod("SetAccent")
        ?? throw new Exception("Missing animated accent switching")).Invoke(null, new object[] { name, Application.Current, animate });
    static Color Ink(string key) => ((SolidColorBrush)Application.Current.FindResource(key)).Color;
    static bool Same(Color a, Color b) => a.A == b.A && a.R == b.R && a.G == b.G && a.B == b.B;
    static double Contrast(Color a, Color b)
    {
        static double L(Color c)
        {
            static double Part(byte n) { double v = n / 255.0; return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
            return 0.2126 * Part(c.R) + 0.7152 * Part(c.G) + 0.0722 * Part(c.B);
        }
        return (Math.Max(L(a), L(b)) + 0.05) / (Math.Min(L(a), L(b)) + 0.05);
    }
    static object? Invoke(MainWindow main, string name, params object[] args) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(main, args);
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    static void Capture(FrameworkElement view, string name)
    {
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            var bounds = new Rect(0, 0, view.ActualWidth, view.ActualHeight);
            drawing.DrawRectangle((Brush)Application.Current.FindResource("CanvasBrush"), null, bounds);
            drawing.DrawRectangle(new VisualBrush(view) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds }, null, bounds);
        }
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string path = Path.Combine(AppContext.BaseDirectory, "Artifacts", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path); encoder.Save(file);
    }
}
