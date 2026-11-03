using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace CodexContinue;

public partial class MainWindow
{
    bool _glassApplied;
    bool _materialMainEntered;
    DispatcherTimer? _glassExit;

    internal void ApplyWindowMaterial(bool enabled)
    {
        UpdateMaterialCoverage();
        _glassExit?.Stop();
        if (!enabled && _glassApplied && ThemeManager.AnimateSurfaces)
        {
            // Keep native blur behind the surface fade until the opaque backing has settled.
            if (_glassExit == null)
            {
                _glassExit = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                _glassExit.Tick += (_, _) => { _glassExit.Stop(); if (!ThemeManager.TransparencyEnabled) SetNativeMaterial(false); };
            }
            _glassExit.Start();
            return;
        }
        SetNativeMaterial(enabled);
    }

    void UpdateMaterialCoverage()
    {
        if (_updateDetailsOpen) return;
        bool covered = ThemeManager.TransparencyEnabled && !_materialMainEntered &&
            (SplashPage.Visibility == Visibility.Visible || ConnectPage.Visibility == Visibility.Visible);
        TabRow.Visibility = ContentHost.Visibility = covered ? Visibility.Collapsed : Visibility.Visible;
    }

    void SetNativeMaterial(bool enabled)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int backdrop = enabled ? 3 : 1; // Desktop Acrylic / no system backdrop.
        bool active = Native.DwmSetWindowAttribute(hwnd, 38, ref backdrop, sizeof(int)) >= 0 && enabled;
        // Full-frame glass exposes the system caption beneath our WPF buttons unless its title-bar style is removed.
        long style = Native.GetWindowLongPtr(hwnd, Native.GWL_STYLE).ToInt64();
        long next = active ? style & ~Native.WS_CAPTION : style | Native.WS_CAPTION;
        if (next != style)
        {
            Native.SetWindowLongPtr(hwnd, Native.GWL_STYLE, new IntPtr(next));
            Native.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED);
        }
        var chrome = WindowChrome.GetWindowChrome(this);
        if (chrome != null)
        {
            if (chrome.IsFrozen) { chrome = (WindowChrome)chrome.Clone(); WindowChrome.SetWindowChrome(this, chrome); }
            chrome.GlassFrameThickness = active ? new Thickness(-1) : new Thickness(0, 0, 0, 1);
        }
        _glassApplied = active;
        SetResourceReference(BackgroundProperty, enabled && !active ? "CanvasBrush" : "WindowCanvasBrush");
        if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target) target.BackgroundColor = Colors.Transparent;
    }

    void Transparency_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded) return;
        _settings.Transparency = TransparencyOn.IsChecked == true;
        _settings.Save();
        ThemeManager.SetTransparency(_settings.Transparency, Application.Current);
    }
}
