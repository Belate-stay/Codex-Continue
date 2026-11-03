using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace CodexContinue;

/// <summary>Full cubic-bezier easing (CSS cubic-bezier semantics). EaseInCore carries
/// the COMPLETE curve, so instances run EasingMode.EaseIn (no WPF-side transform).
/// ⚠ Control points MUST be dependency properties: WPF hands animation clocks a Clone(),
/// which copies only DPs — plain CLR props silently revert to defaults and every curve
/// collapses into one (plus Ease() defaults to EaseOut dispatch = mirrored). Verified
/// on the easetest rig: DP+EaseIn delivers exactly the formula.</summary>
public sealed class BezierEase : EasingFunctionBase
{
    public static readonly DependencyProperty X1Property = DependencyProperty.Register(nameof(X1), typeof(double), typeof(BezierEase), new PropertyMetadata(0.1));
    public static readonly DependencyProperty Y1Property = DependencyProperty.Register(nameof(Y1), typeof(double), typeof(BezierEase), new PropertyMetadata(0.9));
    public static readonly DependencyProperty X2Property = DependencyProperty.Register(nameof(X2), typeof(double), typeof(BezierEase), new PropertyMetadata(0.2));
    public static readonly DependencyProperty Y2Property = DependencyProperty.Register(nameof(Y2), typeof(double), typeof(BezierEase), new PropertyMetadata(1.0));

    public double X1 { get => (double)GetValue(X1Property); set => SetValue(X1Property, value); }
    public double Y1 { get => (double)GetValue(Y1Property); set => SetValue(Y1Property, value); }
    public double X2 { get => (double)GetValue(X2Property); set => SetValue(X2Property, value); }
    public double Y2 { get => (double)GetValue(Y2Property); set => SetValue(Y2Property, value); }

    public BezierEase() => EasingMode = EasingMode.EaseIn;

    protected override Freezable CreateInstanceCore() => new BezierEase();

    protected override double EaseInCore(double t)
    {
        double u = t;
        for (int i = 0; i < 8; i++)
        {
            double x = 3 * u * (1 - u) * (1 - u) * X1 + 3 * u * u * (1 - u) * X2 + u * u * u;
            double dx = 3 * (1 - u) * (1 - u) * X1 + 6 * u * (1 - u) * (X2 - X1) + 3 * u * u * (1 - X2);
            if (Math.Abs(dx) < 1e-6) break;
            u -= (x - t) / dx;
        }
        return 3 * u * (1 - u) * (1 - u) * Y1 + 3 * u * u * (1 - u) * Y2 + u * u * u;
    }
}

public enum ThemeMode { Light, Dark, System }

public static partial class ThemeManager
{
    public static ThemeMode Mode { get; private set; } = ThemeMode.Light; // Office default: light
    public static string Accent { get; private set; } = "blue";

    public static event Action? ThemeChanged;

    static ThemeManager()
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if ((e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color) &&
                (Mode == ThemeMode.System || Accent == "system" || TransparencyRequested)) RefreshSystemPreferences();
        };
        SystemParameters.StaticPropertyChanged += (_, e) => { if (e.PropertyName == nameof(SystemParameters.HighContrast)) RefreshSystemPreferences(); };
    }

    public static void SetMode(ThemeMode mode, Application app)
    {
        Mode = mode;
        ApplyTo(app);
        ThemeChanged?.Invoke();
    }

    public static bool IsDark() => Mode switch
    {
        ThemeMode.Dark => true,
        ThemeMode.Light => false,
        _ => !AppsUseLightTheme(),
    };

    static bool AppsUseLightTheme()
    {
        try
        {
            object? v = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return v is int i ? i != 0 : true;
        }
        catch { return true; }
    }

    public static void ApplyTo(Application app)
    {
        bool dark = IsDark();
        var dict = new ResourceDictionary
        {
            Source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative),
        };
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(dict);
        ApplyAccent(app, animate: false);
        ApplySurfaceBrushes(app, animate: false);
    }

    public static void SetAccent(string accent, Application app, bool animate = true)
    {
        Accent = accent is "blue" or "black" or "green" or "gray" or "pink" or "system" ? accent : "blue";
        ApplyAccent(app, animate);
    }

    static void ApplyAccent(Application app, bool animate)
    {
        bool dark = IsDark();
        var colors = Accent switch
        {
            "black" => dark ? new[] { "#101216", "#FFFFFF", "#D5DAE2", "#292E36", "#697180" }
                            : new[] { "#252930", "#FFFFFF", "#303640", "#EAEDF1", "#252930" },
            "green" => dark ? new[] { "#2D6A50", "#FFFFFF", "#9CD4BA", "#20392D", "#5E9E7E" }
                            : new[] { "#255C47", "#FFFFFF", "#255C47", "#E5F1EA", "#255C47" },
            "gray" => dark ? new[] { "#C3CDD9", "#1B2636", "#D3DCE7", "#2C333D", "#E0E6ED" }
                           : new[] { "#DEE4EC", "#263143", "#42536B", "#EEF1F5", "#ABB8C9" },
            "pink" => dark ? new[] { "#DF90B5", "#361426", "#F4B8D5", "#432A38", "#F3BCD7" }
                           : new[] { "#F0AFCB", "#4B1931", "#99315E", "#FCE9F2", "#DB83AB" },
            _ => dark ? new[] { "#3563D9", "#FFFFFF", "#A5BDFF", "#253357", "#6F91EE" }
                      : new[] { "#3563D9", "#FFFFFF", "#355DCA", "#EAF0FF", "#315AC2" },
        };
        Color fill = (Color)ColorConverter.ConvertFromString(colors[0]);
        Color text = (Color)ColorConverter.ConvertFromString(colors[1]);
        Color ink = (Color)ColorConverter.ConvertFromString(colors[2]);
        Color surface = (Color)ColorConverter.ConvertFromString(colors[3]);
        Color outline = (Color)ColorConverter.ConvertFromString(colors[4]);
        if (Accent == "system")
        {
            var palette = SystemPalette(ReadSystemAccent(), dark);
            fill = palette[0]; text = palette[1]; ink = palette[2]; surface = palette[3]; outline = palette[4];
        }
        Color hover = Blend(surface, fill, 0.12);
        bool fade = animate && SystemParameters.ClientAreaAnimation;
        foreach (var (key, color) in new[]
        {
            ("AccentBrush", fill), ("ModeSelectedBrush", fill), ("AccentTextBrush", text),
            ("AccentInkBrush", ink), ("AccentOutlineBrush", outline),
            ("AccentHoverBrush", Blend(fill, Colors.White, 0.10)),
            ("AccentPressedBrush", Blend(fill, Colors.Black, 0.14)),
            ("ButtonSurfaceBrush", surface), ("KeycapFaceBrush", surface), ("SelectBgBrush", surface),
            ("ButtonHoverBrush", hover), ("ButtonPressedBrush", Blend(surface, fill, 0.20)),
            ("FocusRingBrush", Color.FromArgb(55, ink.R, ink.G, ink.B)),
            ("CaptionHoverBrush", hover),
        })
        {
            PaintBrush(app, key, color, fade);
        }
    }

    static Color Blend(Color from, Color to, double amount) => Color.FromRgb(
        (byte)Math.Round(from.R + (to.R - from.R) * amount),
        (byte)Math.Round(from.G + (to.G - from.G) * amount),
        (byte)Math.Round(from.B + (to.B - from.B) * amount));

    /// <summary>Blend the DWM frame (border + rounded corners) with the active theme.</summary>
    public static void ApplyToWindow(Window w)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(w).Handle;
        if (hwnd == IntPtr.Zero) return;
        int dark = IsDark() ? 1 : 0;
        Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
        if (w is MainWindow main) main.ApplyWindowMaterial(TransparencyEnabled);
    }
}

public static class Motion
{
    /// <summary>CSS ease-in-out — cubic-bezier(0.42,0,0.58,1), the Uiverse button transition.</summary>
    public static IEasingFunction EaseInOut { get; } = new BezierEase { X1 = 0.42, Y1 = 0, X2 = 0.58, Y2 = 1 };

    /// <summary>CSS default ease — cubic-bezier(0.25,0.1,0.25,1), curvy-crab-71's transition.</summary>
    public static IEasingFunction EaseDefault { get; } = new BezierEase { X1 = 0.25, Y1 = 0.1, X2 = 0.25, Y2 = 1 };

    public static readonly Duration Fast = new(TimeSpan.FromMilliseconds(100));
    public static readonly Duration Normal = new(TimeSpan.FromMilliseconds(150));
    public static readonly Duration Slow = new(TimeSpan.FromMilliseconds(250));

    public static IEasingFunction DecelerateMax { get; } = new BezierEase(); // 0.1,0.9,0.2,1

    /// <summary>Page-flow easing: rate starts at zero, lives in the middle, settles at
    /// zero — a full non-linear arc (Material standard 0.4,0,0.2,1), never a slam.</summary>
    public static IEasingFunction Glide { get; } = new BezierEase { X1 = 0.4, Y1 = 0, X2 = 0.2, Y2 = 1 };

    /// <summary>Exit easing, 先慢后快 (Material 3 emphasized-accelerate 0.3,0,0.8,0.15):
    /// departs from rest and accelerates off-screen — the standard curve for elements
    /// leaving, so the page flies out like natural motion instead of sliding at constant speed.</summary>
    public static IEasingFunction AccelerateOff { get; } = new BezierEase { X1 = 0.3, Y1 = 0, X2 = 0.8, Y2 = 0.15 };

    /// <summary>ease-out cubic (0.215,0.61,0.355,1) — clearly 先快后慢 with a soft settle,
    /// for glides the eye must be able to follow (tab pill).</summary>
    public static IEasingFunction SettleOut { get; } = new BezierEase { X1 = 0.215, Y1 = 0.61, X2 = 0.355, Y2 = 1 };

    /// <summary>ease-out quad (0.25,0.46,0.45,0.94) — the gentlest perceptible 先快后慢:
    /// materializes without a pop yet never drags (splash button fade).</summary>
    public static IEasingFunction SoftOut { get; } = new BezierEase { X1 = 0.25, Y1 = 0.46, X2 = 0.45, Y2 = 0.94 };

    /// <summary>ease-out quart (0.165,0.84,0.44,1) — pronounced fast-then-slow (用户：
    /// 速率差加大、先快后慢再明显)：起步 ~5×均速冲刺、中段急减速、末段长尾缓停。</summary>
    public static IEasingFunction QuartOut { get; } = new BezierEase { X1 = 0.165, Y1 = 0.84, X2 = 0.44, Y2 = 1 };

    /// <summary>SoftOut as a pure function for per-frame timer math (no animation clock).</summary>
    public static double SoftOutValue(double t)
    {
        const double x1 = 0.25, y1 = 0.46, x2 = 0.45, y2 = 0.94;
        double u = t;
        for (int i = 0; i < 8; i++)
        {
            double x = 3 * u * (1 - u) * (1 - u) * x1 + 3 * u * u * (1 - u) * x2 + u * u * u;
            double dx = 3 * (1 - u) * (1 - u) * x1 + 6 * u * (1 - u) * (x2 - x1) + 3 * u * u * (1 - x2);
            if (Math.Abs(dx) < 1e-6) break;
            u -= (x - t) / dx;
        }
        return 3 * u * (1 - u) * (1 - u) * y1 + 3 * u * u * (1 - u) * y2 + u * u * u;
    }

    /// <summary>Fade + 8px slide-up entrance, the standard Fluent list/panel reveal.</summary>
    public static void SlideFadeIn(FrameworkElement el, Duration? duration = null, double fromY = 8)
    {
        var d = duration ?? Slow;
        var t = new TranslateTransform();
        el.RenderTransform = t;
        el.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, d) { EasingFunction = DecelerateMax });
        t.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(fromY, 0, d) { EasingFunction = DecelerateMax });
    }
}
