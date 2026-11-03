using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace CodexContinue;

public partial class MainWindow
{
    WindowState _stateBeforeMinimize = WindowState.Normal;

    void BtnMaximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    void UpdateWindowCaption()
    {
        bool maximized = WindowState == WindowState.Maximized;
        if (WindowState != WindowState.Minimized) _stateBeforeMinimize = WindowState;
        MaximizeGlyph.Text = maximized ? "\uE923" : "\uE922";
        BtnMaximize.ToolTip = Loc.T(maximized ? "window.restore" : "window.maximize");
        System.Windows.Automation.AutomationProperties.SetName(BtnMaximize, (string)BtnMaximize.ToolTip);
        var chrome = WindowChrome.GetWindowChrome(this);
        if (chrome != null)
        {
            if (chrome.IsFrozen) { chrome = (WindowChrome)chrome.Clone(); WindowChrome.SetWindowChrome(this, chrome); }
            chrome.ResizeBorderThickness = new Thickness(maximized ? 0 : 6);
        }
        if (maximized) Dispatcher.BeginInvoke(new Action(UpdateMaximizedInsets), DispatcherPriority.Loaded);
        else LayoutRoot.Margin = new Thickness(0);
    }

    void UpdateMaximizedInsets()
    {
        if (WindowState != WindowState.Maximized) return;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var monitor = new Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
        if (!Native.GetMonitorInfo(Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST), ref monitor)) return;
        if (!Native.GetWindowRect(hwnd, out var bounds)) return;
        double scale = Math.Max(96, Native.GetDpiForWindow(hwnd)) / 96d;
        // Keep all content inside the work area while Windows retains its invisible resize frame.
        LayoutRoot.Margin = new Thickness(Math.Max(0, monitor.rcWork.Left - bounds.Left) / scale,
            Math.Max(0, monitor.rcWork.Top - bounds.Top) / scale, Math.Max(0, bounds.Right - monitor.rcWork.Right) / scale,
            Math.Max(0, bounds.Bottom - monitor.rcWork.Bottom) / scale);
    }

    void FitInitialWindowToMonitor(IntPtr hwnd)
    {
        var monitor = new Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
        if (!Native.GetMonitorInfo(Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST), ref monitor)) return;
        double scale = Math.Max(96, Native.GetDpiForWindow(hwnd)) / 96d;
        double width = (monitor.rcWork.Right - monitor.rcWork.Left) / scale;
        double height = (monitor.rcWork.Bottom - monitor.rcWork.Top) / scale;
        MinWidth = Math.Min(640, width);
        MinHeight = Math.Min(480, height);
        Width = Math.Min(Width, width);
        Height = Math.Min(Height, height);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        if (!IsInitialized) return;
        foreach (var element in new UIElement[] { SplashTitle, SplashTitleShadow, BtnStart }.Concat(LoadDots.Children.OfType<UIElement>()))
            if (element.CacheMode is BitmapCache) element.CacheMode = new BitmapCache { RenderAtScale = newDpi.DpiScaleX };
        Dispatcher.BeginInvoke(new Action(() => { UpdateMaximizedInsets(); ApplyWindowLayout(LayoutRoot.RenderSize); }), DispatcherPriority.Loaded);
    }

    void RootLayout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsInitialized && _modeButtons != null) ApplyWindowLayout(e.NewSize);
    }

    void ApplyWindowLayout(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0) return;
        double width = Math.Min(size.Width, 1440);
        bool compact = width < 960;
        RunPage.MinHeight = Math.Min(Math.Max(0, size.Height - 102), 840);
        BoxPrompt.Height = Math.Clamp(136 + (RunPage.MinHeight - 628) * .5, 136, 240);
        CardsRow.MinHeight = compact ? 0 : 430;
        CardsRow.ColumnDefinitions[0].Width = compact ? new GridLength(1, GridUnitType.Star)
            : new GridLength(Math.Clamp((width - 40) * .36, 300, 384));
        CardsRow.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        CardsRow.RowDefinitions[0].Height = compact ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        Grid.SetRow(StatusCard, compact ? 1 : 0);
        Grid.SetColumn(CommandCard, compact ? 0 : 1);
        StatusCard.MinHeight = compact ? 340 : 0;
        CommandCard.MinHeight = compact ? 430 : 0;
        StatusCard.Margin = compact ? new Thickness(0, 16, 0, 0) : new Thickness(0);
        CommandCard.Margin = compact ? new Thickness(0) : new Thickness(16, 0, 0, 0);

        SettingsContent.Margin = new Thickness(compact ? 24 : 56, 10, compact ? 24 : 56, 20);
        foreach (var label in new[] { LblTheme, LblAccent, LblTransparency, LblLang })
        {
            var row = (Grid)label.Parent;
            PairLayout(row, label, (FrameworkElement)row.Children[1], compact, new GridLength(160), new GridLength(1, GridUnitType.Star));
            if (!compact) ((FrameworkElement)row.Children[1]).Margin = new Thickness(0, 10, 0, 10);
        }
        var frequencyLabel = (FrameworkElement)LblDetectionFrequency.Parent;
        var frequencyGrid = (Grid)frequencyLabel.Parent;
        var frequencies = (FrameworkElement)frequencyGrid.Children[1];
        PairLayout(frequencyGrid, frequencyLabel, frequencies, compact, new GridLength(160), new GridLength(1, GridUnitType.Star));
        frequencyLabel.Margin = compact ? new Thickness(0, 6, 0, 0) : new Thickness(0, 10, 16, 0);
        frequencies.Margin = compact ? new Thickness(0, 8, 0, 0) : new Thickness(0);
        foreach (var label in new[] { LblIdleFrequency, LblRunningFrequency })
        {
            var row = (Grid)label.Parent;
            var options = (FrameworkElement)row.Children[1];
            PairLayout(row, label, options, compact, new GridLength(1, GridUnitType.Star), GridLength.Auto);
            options.Width = Math.Min(480, Math.Max(0, width - (compact ? 104 : 328)));
            options.Margin = compact ? new Thickness(0, 8, 0, 0) : new Thickness(0);
        }

        var version = (StackPanel)ValVersion.Parent;
        var about = (Grid)version.Parent;
        if (about.RowDefinitions.Count == 0) { about.RowDefinitions.Add(new()); about.RowDefinitions.Add(new() { Height = GridLength.Auto }); }
        about.ColumnDefinitions[2].Width = compact ? new GridLength(0) : GridLength.Auto;
        version.Orientation = compact ? Orientation.Vertical : Orientation.Horizontal;
        version.Margin = compact ? new Thickness(16, 0, 0, 0) : new Thickness(16, 0, 16, 0);
        ValVersion.TextAlignment = UpdateCheckStatus.TextAlignment = TextAlignment.Right;
        UpdateCheckStatus.Margin = compact ? new Thickness(0, 6, 0, 0) : new Thickness(16, 0, 0, 0);
        Grid.SetRow(BtnCheckUpdates, compact ? 1 : 0);
        Grid.SetColumn(BtnCheckUpdates, compact ? 0 : 2);
        Grid.SetColumnSpan(BtnCheckUpdates, compact ? 3 : 1);
        BtnCheckUpdates.HorizontalAlignment = HorizontalAlignment.Right;
        BtnCheckUpdates.Margin = compact ? new Thickness(0, 12, 0, 0) : new Thickness(0);

        double bodyHeight = Math.Max(0, size.Height - 48);
        double titleSize = size.Width < 720 ? 44 : size.Width < 960 ? 56 : 70;
        SplashTitle.FontSize = SplashTitleShadow.FontSize = titleSize;
        SplashTitle.Margin = SplashTitleShadow.Margin = new Thickness(0, 0, 0, Math.Min(150, bodyHeight * .22));
        BtnStart.Margin = BtnLink.Margin = new Thickness(0, Math.Min(382, bodyHeight * .56), 0, 0);
        ConnectTitle.FontSize = size.Width < 960 ? 30 : 38;
        ConnectHeading.Margin = new Thickness(0, Math.Min(190, bodyHeight * .28), 0, 0);
        LoadDotsHost.Width = LoadDotsHost.Height = Math.Clamp(bodyHeight * .24, 96, 160);
        double center = Math.Min(bodyHeight * .72, bodyHeight - 110);
        LoadDotsHost.Margin = new Thickness(0, Math.Max(0, 2 * center - bodyHeight), 0, 0);
        ConvoPick.IsDropDownOpen = false;
        UpdateNavigation(animate: false);
    }

    static void PairLayout(Grid row, FrameworkElement label, FrameworkElement choices, bool compact, GridLength labelWidth, GridLength choicesWidth)
    {
        if (row.RowDefinitions.Count == 0)
        {
            row.RowDefinitions.Add(new() { Height = GridLength.Auto });
            row.RowDefinitions.Add(new() { Height = GridLength.Auto });
        }
        row.RowDefinitions[0].Height = compact ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        row.ColumnDefinitions[0].Width = compact ? new GridLength(1, GridUnitType.Star) : labelWidth;
        row.ColumnDefinitions[1].Width = compact ? new GridLength(0) : choicesWidth;
        Grid.SetRow(choices, compact ? 1 : 0);
        Grid.SetColumn(choices, compact ? 0 : 1);
        label.Margin = compact ? new Thickness(0, 12, 0, 0) : new Thickness(0);
        choices.Margin = compact ? new Thickness(0, 8, 0, 12) : new Thickness(0);
    }
}
