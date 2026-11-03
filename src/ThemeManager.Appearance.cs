using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace CodexContinue;

public static partial class ThemeManager
{
    public static bool TransparencyRequested { get; private set; }
    public static bool TransparencyEnabled { get; private set; }
    internal static bool AnimateSurfaces { get; private set; }

    public static void SetTransparency(bool enabled, Application app, bool animate = true)
    {
        TransparencyRequested = enabled;
        ApplySurfaceBrushes(app, animate);
        ThemeChanged?.Invoke();
    }

    static void ApplySurfaceBrushes(Application app, bool animate)
    {
        TransparencyEnabled = TransparencyRequested && Environment.OSVersion.Version.Build >= 22621 &&
            !SystemParameters.HighContrast && Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1) is int value && value != 0;
        AnimateSurfaces = animate && SystemParameters.ClientAreaAnimation;
        bool glass = TransparencyEnabled, dark = IsDark();
        Color canvas = (Color)app.FindResource("Canvas"), well = (Color)app.FindResource("Well");
        PaintBrush(app, "WindowCanvasBrush", Alpha(canvas, glass ? (byte)(dark ? 80 : 60) : (byte)255), AnimateSurfaces);
        PaintBrush(app, "PageCanvasBrush", Alpha(canvas, glass ? (byte)0 : (byte)255), AnimateSurfaces);
        PaintBrush(app, "WellBrush", Alpha(well, glass ? (byte)(dark ? 208 : 200) : (byte)255), AnimateSurfaces);
        PaintBrush(app, "CardShadowBrush", Alpha(well, glass ? (byte)44 : (byte)255), AnimateSurfaces);
        PaintBrush(app, "TextSecondaryBrush", glass ? (Color)ColorConverter.ConvertFromString(dark ? "#BBC1CC" : "#505866") : (Color)app.FindResource("TextSecondary"), AnimateSurfaces);
        PaintBrush(app, "TextTertiaryBrush", glass ? (Color)ColorConverter.ConvertFromString(dark ? "#A5AFBD" : "#586270") : (Color)app.FindResource("TextTertiary"), AnimateSurfaces);
    }

    static Color Alpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    static void PaintBrush(Application app, string key, Color color, bool animate)
    {
        var brush = app.Resources[key] as SolidColorBrush;
        Color from = brush?.Color ?? color;
        bool replace = !animate || brush == null || brush.IsFrozen;
        if (replace) brush = new SolidColorBrush(animate ? from : color);
        else brush!.Color = from;
        // Publish an unfrozen animated brush so interrupted fades continue from the displayed color.
        brush!.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(animate ? from : color, color, TimeSpan.FromMilliseconds(animate ? 320 : 0))
                { EasingFunction = Motion.SettleOut });
        if (replace) app.Resources[key] = brush;
    }

    internal static Color ReadSystemAccent()
    {
        if (Native.DwmGetColorizationColor(out uint value, out _) >= 0)
            return Color.FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return Color.FromRgb(53, 99, 217);
    }

    static Color[] SystemPalette(Color source, bool dark)
    {
        Color fill = Alpha(source, 255);
        static double Linear(byte n) { double v = n / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
        double luminance = .2126 * Linear(fill.R) + .7152 * Linear(fill.G) + .0722 * Linear(fill.B);
        Color text = luminance > .179 ? Colors.Black : Colors.White;
        Color ink = Blend(fill, dark ? Colors.White : Colors.Black, .64);
        Color surface = Blend(dark ? Color.FromRgb(28, 31, 37) : Colors.White, fill, dark ? .18 : .10);
        return new[] { fill, text, ink, surface, ink };
    }

    static void RefreshSystemPreferences()
    {
        var app = Application.Current;
        if (app == null || app.Dispatcher.HasShutdownStarted) return;
        app.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (Mode == ThemeMode.System) ApplyTo(app);
            else
            {
                if (Accent == "system") ApplyAccent(app, animate: true);
                ApplySurfaceBrushes(app, animate: false);
            }
            ThemeChanged?.Invoke();
        }));
    }
}
