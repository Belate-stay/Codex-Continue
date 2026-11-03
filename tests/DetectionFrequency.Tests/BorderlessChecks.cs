using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexContinue;

static class BorderlessChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    public static void Surface()
    {
        var context = SynchronizationContext.Current;
        var theme = ThemeManager.Mode; var accent = ThemeManager.Accent; var lang = Loc.Current;
        var main = new MainWindow { ShowActivated = false, Title = "Borderless surface regression" };
        UpdateChecks.IsolateLoadedWindow(main);
        typeof(MainWindow).GetField("_scanTask", Private)!.SetValue(main, Task.CompletedTask);
        try
        {
            main.Show();
            Find<Grid>(main, "SplashPage").Visibility = Visibility.Collapsed;
            Find<TextBlock>(main, "TitleName").Visibility = Find<Image>(main, "TitleIcon").Visibility = Visibility.Visible;
            Find<Grid>(main, "LayoutRoot").SetResourceReference(Panel.BackgroundProperty, "CanvasBrush");
            Invoke(main, "DismissUpdateToast"); Pump(220);
            foreach (var appearance in new[] { CodexContinue.ThemeMode.Light, CodexContinue.ThemeMode.Dark })
            foreach (string color in new[] { "blue", "black", "pink" })
            {
                ThemeManager.SetMode(appearance, Application.Current);
                ThemeManager.SetAccent(color, Application.Current, animate: false);
                Loc.Set(appearance == CodexContinue.ThemeMode.Light ? Loc.Lang.Zh : Loc.Lang.En); Invoke(main, "ApplyLoc");
                foreach (string view in new[] { "Auto", "Manual", "Settings" })
                {
                    Invoke(main, "SwitchView", Enum.Parse(typeof(MainWindow).GetNestedType("ViewMode", BindingFlags.NonPublic)!, view));
                    Pump(500);
                    var root = Find<Grid>(main, "LayoutRoot");
                    foreach (var border in Descendants(root).OfType<Border>().Where(Presented))
                        Require(!VisibleStroke(border), $"Visible solid outline on {border.Name} in {appearance}/{color}/{view}");
                    if (color == "blue") Capture(root, $"borderless-{appearance}-{view}.png");
                }
                foreach (string name in new[] { "BtnRecheck", "BtnRun", "BtnCheckUpdates", "BtnAttach" })
                {
                    var button = Find<Button>(main, name); button.ApplyTemplate();
                    var face = (Border)button.Template.FindName("bd", button);
                    var caster = (Border)button.Template.FindName("caster", button);
                    Require(face.Effect == null && caster.Effect is DropShadowEffect && !caster.IsAncestorOf(face),
                        $"{name} has no soft depth or rasterizes its text inside a shadow effect");
                }
                var selected = new[] { "ThemeSystem", "ThemeLight", "ThemeDark" }.Select(n => Find<RadioButton>(main, n)).Single(b => b.IsChecked == true);
                var selection = (Border)selected.Template.FindName("checkOverlay", selected);
                Require(!VisibleStroke(selection) && selection.Effect == null && selection.Opacity > .99,
                    "A selected settings option has a hard outline or unnecessary shadow");
                var focus = new Control { Style = (Style)Application.Current.FindResource("SoftFocusCue"), Width = 100, Height = 40 };
                focus.ApplyTemplate();
                var halo = (Border)VisualTreeHelper.GetChild(focus, 0);
                Require(!VisibleStroke(halo) && halo.Background != null && halo.Effect is BlurEffect,
                    "Keyboard focus is missing or still uses a solid outline");
            }
        }
        finally
        {
            typeof(MainWindow).GetField("_forceClose", Private)!.SetValue(main, true); main.Close();
            Loc.Set(lang); ThemeManager.SetMode(theme, Application.Current); ThemeManager.SetAccent(accent, Application.Current, animate: false);
            SynchronizationContext.SetSynchronizationContext(context);
        }
    }

    static bool VisibleStroke(Border border) => border.BorderThickness != new Thickness(0) && border.BorderBrush is { Opacity: > 0 } brush &&
        (brush is not SolidColorBrush solid || solid.Color.A != 0);
    static bool Presented(Border border)
    {
        if (!border.IsVisible) return false;
        double opacity = 1;
        for (DependencyObject? current = border; current != null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement element) opacity *= element.Opacity;
        return opacity > .01;
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    static T Find<T>(MainWindow main, string name) where T : class => (T)main.FindName(name);
    static void Invoke(MainWindow main, string method, params object[] args) => typeof(MainWindow).GetMethod(method, Private)!.Invoke(main, args);
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    static void Capture(FrameworkElement root, string name)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string folder = Path.Combine(AppContext.BaseDirectory, "Artifacts"); Directory.CreateDirectory(folder);
        using var file = File.Create(Path.Combine(folder, name)); encoder.Save(file);
    }
}
