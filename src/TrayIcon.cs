using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;
using Path = System.Windows.Shapes.Path;

namespace CodexContinue;

sealed class TrayIcon : IDisposable
{
    readonly Forms.NotifyIcon _icon;
    readonly ContextMenu _menu;
    readonly MenuItem _open = new TrayMenuItem();
    readonly MenuItem _settings = new TrayMenuItem();
    readonly MenuItem _run = new TrayMenuItem();
    readonly MenuItem _exit = new TrayMenuItem();
    readonly Path _runSymbol = new() { Width = 12, Height = 12, Stretch = Stretch.Uniform };
    readonly System.Drawing.Icon _image;
    static readonly Geometry Play = Geometry.Parse("M3,1 L13,7 L3,13 Z");
    static readonly Geometry Stop = new RectangleGeometry(new Rect(1, 1, 12, 12), 1, 1);
    bool _running;
    bool _disposed;

    public TrayIcon(Action restore, Action settings, Action toggleRun, Action exit)
    {
        Forms.Application.EnableVisualStyles();
        using var stream = Application.GetResourceStream(
            new Uri("pack://application:,,,/CodexContinue;component/app.ico")).Stream;
        _image = new System.Drawing.Icon(stream);
        var resources = new ResourceDictionary { Source = new Uri("/CodexContinue;component/TrayMenu.xaml", UriKind.Relative) };
        _menu = new ContextMenu
        {
            Style = (Style)resources["TrayContextMenu"],
            ItemContainerStyle = (Style)resources["TrayMenuItem"],
            Placement = PlacementMode.MousePoint,
            StaysOpen = false,
        };
        _runSymbol.SetResourceReference(Shape.FillProperty, "ErrBrush");
        _run.Icon = _runSymbol;
        ToolTipService.SetShowOnDisabled(_run, true);
        _menu.Items.Add(_open);
        _menu.Items.Add(_settings);
        _menu.Items.Add(_run);
        _menu.Items.Add(_exit);
        Bind(_open, restore);
        Bind(_settings, settings);
        Bind(_run, toggleRun);
        Bind(_exit, exit);
        _menu.Opened += (_, _) =>
        {
            // Reactivating a popup while our window already owns foreground can reset WPF focus and close it.
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out uint foregroundProcess);
            if (foregroundProcess != Environment.ProcessId && PresentationSource.FromVisual(_menu) is HwndSource source)
                Native.SetForegroundWindow(source.Handle);
            _menu.Focus();
            if (SystemParameters.ClientAreaAnimation)
                _menu.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
                    { EasingFunction = Motion.SoftOut });
        };
        _icon = new Forms.NotifyIcon
        {
            Icon = _image,
            Text = "Codex-Continue",
            Visible = true,
        };
        _icon.MouseUp += (_, e) => { if (e.Button == Forms.MouseButtons.Right) ShowMenu(); };
        _icon.MouseDoubleClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) restore(); };
        UpdateState(running: false, canRun: false);
    }

    void Bind(MenuItem item, Action action) => item.Click += (_, _) =>
    {
        if (!item.IsEnabled) return;
        _menu.IsOpen = false;
        action();
    };

    void ShowMenu()
    {
        if (_disposed) return;
        _menu.IsOpen = false;
        _menu.UpdateDefaultStyle();
        ApplyLanguage();
        _menu.BeginAnimation(UIElement.OpacityProperty, null);
        _menu.Opacity = 1;
        _menu.IsOpen = true;
    }

    internal void UpdateState(bool running, bool canRun)
    {
        _running = running;
        _run.IsEnabled = running || canRun;
        _runSymbol.Data = running ? Stop : Play;
        ApplyLanguage();
    }

    public void ApplyLanguage()
    {
        _open.Header = Loc.T("tray.open");
        _settings.Header = Loc.T("settings.title");
        _run.Header = Loc.T(_running ? "cmd.stop" : "cmd.run");
        _run.ToolTip = _run.IsEnabled ? null : Loc.T("cmd.incomplete");
        _exit.Header = Loc.T("tray.exit");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _menu.IsOpen = false;
        _menu.BeginAnimation(UIElement.OpacityProperty, null);
        _menu.Items.Clear();
        _icon.Visible = false;
        _icon.Dispose();
        _image.Dispose();
    }
}

sealed class TrayMenuItem : MenuItem
{
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property != IsHighlightedProperty || Template?.FindName("TrayItemHover", this) is not Border face) return;
        double from = face.Opacity, to = IsHighlighted ? 1 : 0;
        face.BeginAnimation(OpacityProperty, null);
        face.Opacity = to;
        if (SystemParameters.ClientAreaAnimation)
            face.BeginAnimation(OpacityProperty, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(IsHighlighted ? 120 : 160))
                { EasingFunction = Motion.QuartOut });
    }
}
