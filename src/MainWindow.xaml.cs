using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CodexContinue;

public partial class MainWindow : Window
{
    enum RunMode { Auto, Manual }
    enum ViewMode { Auto, Manual, Log, Settings }

    /// <summary>连接三态（用户口径）：①Codex 正常=账号可读+额度可信；②连上但没网=
    /// 账号名拿不到、额度不可信；③没连上=离线。null=尚未分类（首扫前）。</summary>
    enum ConnState { Online, NoNetwork, Offline }
    ConnState? _conn;

    readonly DispatcherTimer _scanTimer = new();
    readonly DispatcherTimer _manualTimer = new();
    readonly AppSettings _settings = AppSettings.Load();
    System.Threading.Tasks.Task? _scanTask;

    QuotaSnapshot _snap = QuotaSnapshot.Unknown("尚未检测");
    List<CodexService.RunningThread> _threads = new();
    RateLimitsClient.AccountInfo? _account;

    RunMode _mode = RunMode.Auto;
    ViewMode _view = ViewMode.Auto;
    int _viewRevision;
    int _runModeRevision;
    readonly MorphRunner _settingsMorph;
    readonly MorphRunner _logMorph;
    readonly RadioButton[] _modeButtons;
    DoubleAnimation? _modeSlide;
    bool _updatingNavigation;
    bool _armed;
    bool _sending;
    int _runRevision;
    Func<string, string?, System.Threading.Tasks.Task<CodexService.SendResult>> _sendQueued = CodexService.QueueSend;
    Func<string, System.Threading.Tasks.Task<CodexService.SendResult>> _sendFallback = message => CodexService.SendContinue(message);
    bool _firedThisCycle;
    int _sendAttempts;
    int _blockedAttempts;
    DateTime _retryAfter = DateTime.MinValue;
    DateTime? _manualSendAt;
    bool _sendTimeTouched;            // user edited the manual send time; stop auto-filling from ResetAt
    DateTime? _exhaustedAt;           // auto mode: when the current exhaustion started
    DateTime? _target;                // auto mode: estimated recovery moment
    bool _sawExhausted;
    bool _loading;                    // guards radio Checked events during init
    bool _scanning;                   // recheck button busy guard
    double _lastPct = -1;
    readonly SolidColorBrush _quotaInk = new();
    string? _quotaInkKey;
    DispatcherTimer? _quotaDigitTimer;
    DateTime _lastDetectAt;           // every detection attempt stamps this (offline too)
    bool _revealing;                  // quota digit-roll animation in flight
    bool _displayUnknown = true;      // what the hero currently shows (unknown→number first-appearance)
    bool _entryRevealPending;         // 随机揭示只留「进入首页即见数字」这一处（用户 10-31 口径）
    int _revealDelayMs;               // entry-from-connect: delay reveal so the slide uncovers the churn
    bool _foundPending;               // connect page: Codex found, waiting out the 5s floor
    QuotaSnapshot? _prefetched;       // quota read during the connect page, ready at entry

    readonly DispatcherTimer _connectTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    DateTime _connectDeadline;
    DateTime _connectOpenedAt;
    bool _probing;                    // FindWindow sweep in flight (off-thread)

    static readonly FontFamily OfflineHeroFont = new("DengXian, Microsoft YaHei UI");
    int _dotTick;

    string? _targetThreadId;
    string _targetThreadName = "";
    readonly List<string> _attachments = new();
    bool _forceClose;
    bool _started;
    TrayIcon? _tray;

    public MainWindow()
    {
        InitializeComponent();
        _settingsMorph = new MorphRunner(IconGear, MorphIcons.Settings, MorphIcons.Settings2);
        _logMorph = new MorphRunner(IconLog, MorphIcons.ScrollText, MorphIcons.FileText);
        _modeButtons = new[] { TabAuto, TabManual, BtnLog, BtnSettings };
        UpdateNavigation(animate: false);

        // no opacity fade-in: a semi-transparent window shows whatever sits behind it
        // (that see-through beat is the "flash" users see) — the splash must land solid

        Loc.Set(_settings.ToLang());
        ThemeManager.SetMode(_settings.ToThemeMode(), Application.Current);
        ThemeManager.SetAccent(_settings.Accent, Application.Current, animate: false);
        ThemeManager.SetTransparency(_settings.Transparency, Application.Current, animate: false);
        LayoutRoot.Loaded += (_, _) => UpdateMaterialCoverage();
        SplashPage.IsVisibleChanged += (_, _) => UpdateMaterialCoverage();
        ConnectPage.IsVisibleChanged += (_, _) => UpdateMaterialCoverage();
        BoxPrompt.Text = "/Continue";
        QuotaFill.Background = _quotaInk;
        QuotaPct.Foreground = _quotaInk;
        QuotaSign.Foreground = _quotaInk;
        UpdateQuotaColor(100, animate: false);
        PreviewMouseDown += Root_PreviewMouseDown;

        _scanTimer.Tick += (_, _) => _ = ScanAsync();
        _manualTimer.Tick += ManualTimer_Tick;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Native.EnableNativeAnimations(hwnd);
            int corner = Native.DWMWCP_ROUND; // Win11 rounded corners for the frameless window
            Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, 4);
            ThemeManager.ApplyToWindow(this);
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
            FitInitialWindowToMonitor(hwnd);
            _tray = new TrayIcon(RestoreFromTray, SettingsFromTray, ToggleRunFromTray, ExitFromTray);
            UpdateCommandState();
        };

        StateChanged += (_, _) => UpdateWindowCaption();

        Loaded += (_, _) =>
        {
            if (_started) return;
            _started = true;
            CodexService.DebugSelfCheck();
            ApplyLoc();
            ApplyWindowLayout(LayoutRoot.RenderSize);
            SplashEnter();
            MorphIcons.SelfCheck();

            // The splash remains the first screen; idle detection follows the saved frequency.
            // the splash carries its own art title — the chrome icon+name stay hidden.
            // (the old 250ms SlideFadeIn entrance is GONE: it used to replace the
            // SplashEnter fade mid-flight = the "too fast + flicker" users saw)
            TitleIcon.Visibility = Visibility.Collapsed;
            TitleName.Visibility = Visibility.Collapsed;
            _connectTimer.Tick += ConnectTick;
            UpdateScanInterval();
            StartUpdateCheck();
        };

        Closing += (_, e) =>
        {
            if (!_forceClose)
            {
                e.Cancel = true;
                FinishStartupTransition();
                ConvoPick.IsDropDownOpen = false;
                Hide();
            }
        };

        Application.Current.SessionEnding += App_SessionEnding;
        Closed += (_, _) =>
        {
            FinishStartupTransition();
            _updatesCancellation.Cancel();
            _glassExit?.Stop();
            _updates.Dispose();
            Application.Current.SessionEnding -= App_SessionEnding;
            _scanTimer.Stop();
            _manualTimer.Stop();
            _connectTimer.Stop();
            StopLoadingDots();
            StopQuotaDigits();
            CompositionTarget.Rendering -= ModePill_Rendering;
            _tray?.Dispose();
            Native.SetThreadExecutionState(Native.ES_CONTINUOUS);
        };

        ThemeManager.ThemeChanged += () =>
        {
            ThemeManager.ApplyToWindow(this);
            UpdateQuotaColor(CurrentQuotaPercent, animate: false, force: true);
        };
    }

    // ---------- native window ----------
    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_DWMCOLORIZATIONCOLORCHANGED && ThemeManager.Accent == "system")
            Dispatcher.BeginInvoke(new Action(() => ThemeManager.SetAccent("system", Application.Current)));
        if (msg == Native.WM_SETTINGCHANGE && WindowState == WindowState.Maximized)
            Dispatcher.BeginInvoke(new Action(UpdateMaximizedInsets), DispatcherPriority.Loaded);
        if (msg == Native.WM_GETMINMAXINFO) // use this monitor's physical work-area bounds, including negative origins
        {
            var mmi = Marshal.PtrToStructure<Native.MINMAXINFO>(lParam);
            var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
            if (Native.GetMonitorInfo(Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST), ref mi))
            {
                mmi.ptMaxPosition.X = mi.rcWork.Left - mi.rcMonitor.Left;
                mmi.ptMaxPosition.Y = mi.rcWork.Top - mi.rcMonitor.Top;
                mmi.ptMaxSize.X = mi.rcWork.Right - mi.rcWork.Left;
                mmi.ptMaxSize.Y = mi.rcWork.Bottom - mi.rcWork.Top;
                double scale = Math.Max(96, Native.GetDpiForWindow(hwnd)) / 96d;
                mmi.ptMinTrackSize.X = (int)Math.Min(mmi.ptMaxSize.X, Math.Ceiling(MinWidth * scale));
                mmi.ptMinTrackSize.Y = (int)Math.Min(mmi.ptMaxSize.Y, Math.Ceiling(MinHeight * scale));
                Marshal.StructureToPtr(mmi, lParam, true);
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    void BtnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    // curvy-crab hover: the twin bars sweep in from opposite edges (default ease,
    // same both ways). 250ms — 用户：速率放快、一放上就立即播
    void SkipWrap_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        foreach (var t in new[] { SkipBarTop, SkipBarBot })
            ((ScaleTransform)t.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1, TimeSpan.FromMilliseconds(250)) { EasingFunction = Motion.EaseDefault });
    }

    void SkipWrap_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        foreach (var t in new[] { SkipBarTop, SkipBarBot })
            ((ScaleTransform)t.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(0, TimeSpan.FromMilliseconds(250)) { EasingFunction = Motion.EaseDefault });
    }

    void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    internal void RestoreFromTray()
    {
        if (_forceClose) return;
        Show();
        if (WindowState == WindowState.Minimized) WindowState = _stateBeforeMinimize;
        Activate();
        CodexService.ForceForeground(new WindowInteropHelper(this).Handle);
    }

    void ExitFromTray()
    {
        _forceClose = true;
        Close();
    }

    void SettingsFromTray()
    {
        FinishStartupTransition();
        RestoreFromTray();
        if (SplashPage.Visibility == Visibility.Visible || ConnectPage.Visibility == Visibility.Visible)
        {
            _connectTimer.Stop();
            StopLoadingDots();
            SplashPage.Visibility = ConnectPage.Visibility = Visibility.Collapsed;
            TitleIcon.Visibility = TitleName.Visibility = Visibility.Visible;
        }
        if (_updateDetailsOpen) BtnUpdateBack_Click(this, new RoutedEventArgs());
        SwitchView(ViewMode.Settings);
    }

    void ToggleRunFromTray()
    {
        bool starting = !_armed;
        BtnRun_Click(this, new RoutedEventArgs());
        if (starting && !_armed && HintDialog.Visibility == Visibility.Visible) RestoreFromTray();
    }

    void App_SessionEnding(object sender, SessionEndingCancelEventArgs e) => _forceClose = true;

    void BoxPrompt_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePromptHint();
        UpdateCommandState();
    }

    bool CommandIsComplete => ConvoPick.SelectedItem is CodexService.RunningThread
        && !string.IsNullOrWhiteSpace(BoxPrompt.Text);

    void UpdateCommandState()
    {
        if (!_armed)
        {
            var selected = ConvoPick.SelectedItem as CodexService.RunningThread;
            _targetThreadId = selected?.Id;
            _targetThreadName = selected?.Name ?? "";
        }
        bool enabled = _armed || CommandIsComplete;
        BtnRun.IsEnabled = enabled;
        BtnRun.ToolTip = enabled ? null : Loc.T("cmd.incomplete");
        _tray?.UpdateState(_armed, enabled);
    }

    /// <summary>用户口径：蓝框只跟提示词焦点走；占位字=为空 且 未聚焦 才显示；
    /// 点击提示词以外的页面区域即收框（清键盘焦点）。
    /// 过渡：蓝框自框缘向外延伸 2px 同步淡入（退场反向收回）——三个状态一条动画线。</summary>
    void BoxPrompt_FocusChanged(object sender, RoutedEventArgs e)
    {
        bool focused = BoxPrompt.IsKeyboardFocusWithin;
        var d = TimeSpan.FromMilliseconds(focused ? 150 : 120); // 秒值曾误写进毫秒参数（0.15ms）=过渡硬闪
        PromptRing.BeginAnimation(OpacityProperty,
            new DoubleAnimation(focused ? 1 : 0, d) { EasingFunction = Motion.SoftOut });
        PromptRing.BeginAnimation(MarginProperty,
            new ThicknessAnimation(new Thickness(focused ? -2 : 0), d) { EasingFunction = Motion.SoftOut });
        UpdatePromptHint();
    }

    void Root_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!BoxPrompt.IsKeyboardFocusWithin) return;
        if (IsDescendant(e.OriginalSource as DependencyObject, BoxPrompt)) return;
        System.Windows.Input.Keyboard.ClearFocus(); // 点外部 → 收蓝框、占位字回归
    }

    static bool IsDescendant(DependencyObject? d, DependencyObject root)
    {
        while (d != null)
        {
            if (d == root) return true;
            d = VisualTreeHelper.GetParent(d);
        }
        return false;
    }

    void UpdatePromptHint() =>
        PromptHint.Visibility = BoxPrompt.Text.Length == 0 && !BoxPrompt.IsKeyboardFocusWithin
            ? Visibility.Visible
            : Visibility.Collapsed;

    // ---------- localization ----------
    /// <summary>Field label = bold solid bullet + text (the old "·" middle dot read as a speck).</summary>
    static void SetFieldLabel(TextBlock tb, string text)
    {
        tb.Inlines.Clear();
        var bullet = new Run("● ")
        {
            FontWeight = FontWeights.Bold,
            FontSize = 10,
            FontFamily = new FontFamily("Microsoft YaHei UI, Microsoft YaHei"),
        };
        bullet.SetResourceReference(Run.ForegroundProperty, "TextPrimaryBrush");
        tb.Inlines.Add(bullet);
        tb.Inlines.Add(new Run(text));
    }

    void ApplyLoc()
    {
        TabAuto.Content = Loc.T("tab.auto");
        TabManual.Content = Loc.T("tab.manual");
        QuotaLabel.Text = Loc.T("quota.window");
        LblDetect.Text = Loc.T("quota.detectTime");
        TitleStatus.Text = Loc.T("status.title");
        LblWindow.Text = Loc.T("status.codexWindow");
        LblAccount.Text = Loc.T("status.account");
        LblConvos.Text = Loc.T("status.runningConvos");
        Application.Current.Resources["ThreadRunningText"] = Loc.T("status.threadRunning");
        Application.Current.Resources["ConversationPlaceholderText"] = Loc.T("cmd.selectConversation");
        LblQuota.Text = Loc.T("status.quota");
        LblReset.Text = Loc.T("status.reset");
        LblSource.Text = Loc.T("status.weekly");
        TitleCommand.Text = Loc.T("cmd.title");
        SetFieldLabel(LblPrompt, Loc.T("cmd.prompt"));
        SetFieldLabel(LblAttach, Loc.T("cmd.attachment"));
        SetFieldLabel(LblSendTime, Loc.T("cmd.sendTime"));
        SetFieldLabel(LblConvo, Loc.T("cmd.conversation"));
        LoopSend.Content = Loc.T("cmd.loopSend");
        LoopHelpText.Text = Loc.T("cmd.loopHelp");
        System.Windows.Automation.AutomationProperties.SetHelpText(LoopSend, Loc.T("cmd.loopHelp"));
        BtnRecheck.Content = Loc.T("cmd.recheck");
        BtnRecheck.ToolTip = Loc.T("cmd.recheckHint");
        BtnStart.Content = Loc.T("splash.start");
        BtnLink.Content = Loc.T("connect.retry");
        SkipLink.Text = Loc.T("connect.skip");
        ConnectTitle.Text = Spread(Loc.T("connect.title"), ' ');
        ConnectMsg.Text = Spread(Loc.T("connect.bottom"), ' ');
        BtnRun.Content = _armed ? Loc.T("cmd.stop") : Loc.T("cmd.run");
        UpdateCommandState();
        PromptHint.Text = Loc.T("cmd.placeholder.prompt");
        UpdatePromptHint();
        BtnAttach.ToolTip = Loc.T("cmd.addAttachment");
        LblGeneralSettings.Text = Loc.T("settings.general");
        LblConnectionSettings.Text = Loc.T("settings.connection");
        LblAboutSettings.Text = Loc.T("settings.about");
        LblVersion.Text = Loc.T("settings.version");
        ValVersion.Text = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        BtnGithub.ToolTip = Loc.T("github.repository");
        System.Windows.Automation.AutomationProperties.SetName(BtnGithub, Loc.T("github.repository"));
        BtnSettings.ToolTip = Loc.T("settings.title");
        BtnLog.ToolTip = Loc.T("log.title");
        System.Windows.Automation.AutomationProperties.SetName(BtnSettings, Loc.T("settings.title"));
        System.Windows.Automation.AutomationProperties.SetName(BtnLog, Loc.T("log.title"));
        LblTheme.Text = Loc.T("settings.theme");
        LblAccent.Text = Loc.T("settings.accent");
        LblTransparency.Text = Loc.T("settings.transparency");
        LblTransparency.ToolTip = Loc.T("settings.transparencyHint");
        TransparencyOff.Content = Loc.T("settings.off");
        TransparencyOn.Content = Loc.T("settings.on");
        foreach (RadioButton option in AccentOptions.Children)
        {
            string label = Loc.T("settings.accent." + (string)option.Tag);
            if ((string)option.Tag == "system") option.Content = label;
            else ((TextBlock)((StackPanel)option.Content).Children[1]).Text = label;
            System.Windows.Automation.AutomationProperties.SetName(option, label);
        }
        LblLang.Text = Loc.T("settings.language");
        LblDetectionFrequency.Text = Loc.T("settings.detectionFrequency");
        LblIdleFrequency.Text = Loc.T("settings.idle");
        LblRunningFrequency.Text = Loc.T("settings.running");
        IdleFrequencyHint.Text = Loc.T("settings.idleHint");
        FrequencyApplied.Text = Loc.T("settings.applied");
        foreach (var panel in new Panel[] { IdleFrequencyOptions, RunningFrequencyOptions })
        {
            foreach (RadioButton option in panel.Children)
            {
                int seconds = int.Parse((string)option.Tag, CultureInfo.InvariantCulture);
                string text = seconds == 0 ? Loc.T("settings.realtime")
                    : seconds < 60 ? Loc.T("settings.seconds", seconds)
                    : Loc.T("settings.minutes", seconds / 60);
                option.Content = text;
                System.Windows.Automation.AutomationProperties.SetName(option,
                    Loc.T(panel == IdleFrequencyOptions ? "settings.idle" : "settings.running") + ": " + text);
            }
        }
        UpdateDetectionHint();
        ThemeSystem.Content = Loc.T("settings.followSystem");
        ThemeLight.Content = Loc.T("settings.light");
        ThemeDark.Content = Loc.T("settings.dark");
        LangSystem.Content = Loc.T("settings.followSystem");
        LangZh.Content = Loc.T("settings.zh");
        LangEn.Content = Loc.T("settings.en");
        TitleLog.Text = Loc.T("log.title");
        BtnClearLog.Content = Loc.T("log.clear");
        LogEmptyHint.Text = Loc.T("log.empty");
        _tray?.ApplyLanguage();
        HintTitle.Text = Loc.T("dialog.hintTitle");
        HintOk.Content = Loc.T("dialog.ok");
        HintCancel.Content = Loc.T("dialog.cancel");
        ApplyUpdateLoc();
        UpdateWindowCaption();
        Dispatcher.BeginInvoke(new Action(() => UpdateNavigation(animate: false)), DispatcherPriority.Loaded);
    }

    readonly System.Collections.Generic.List<Storyboard> _dotSbs = new();

    // Windows boot ring — verbatim windows_10.css (JKDev): 5 dots orbiting, 4.8s loop,
    // 240ms stagger, rotation keyframes 0/7/30/39/70/75% at 225/345/455/690/815/945deg
    // with the CSS timing functions 1:1 (ease-out / linear / ease-in-out / linear /
    // ease-out); each dot fades out 70→76% at the lap end and pops back in at 225deg
    // on the next loop. Ring centred in the canvas, Win10 proportions: orbit Ø54px
    // with 5.5px dots (the CSS's 5px-on-53.7px ratio), cached bitmaps for crisp orbits.
    void StartLoadingDots()
    {
        StopLoadingDots();
        const double C = 80, R = 27, Tau = 0.24;  // canvas centre / orbit radius / stagger
        var easeOut = new KeySpline(0, 0, 0.58, 1);       // CSS ease-out
        var easeInOut = new KeySpline(0.42, 0, 0.58, 1);  // CSS ease-in-out
        var linear = new KeySpline(0, 0, 1, 1);           // CSS linear

        for (int k = 0; k < 5; k++)
        {
            var dot = new System.Windows.Shapes.Ellipse { Width = 5.5, Height = 5.5 };
            dot.SetResourceReference(System.Windows.Shapes.Ellipse.FillProperty, "TextPrimaryBrush");
            // faint soft shadow riding along the orbit (Effect.Opacity carries the
            // transparency — DropShadowEffect.Color's alpha channel does not render)
            dot.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Color.FromRgb(0x1A, 0x22, 0x33),
                Opacity = 0.25,
                BlurRadius = 4,
                ShadowDepth = 1.5,
            };
            // seat at the orbit's 225deg point (the CSS corner start); the rotation moves it.
            // RotateTransform centres live in the ELEMENT's own space (origin = its top-left),
            // so the ring centre must be rebased to the dot's corner or the orbit skews.
            double a = 225.0 * Math.PI / 180.0;
            double seatX = C + R * Math.Cos(a) - 2.75, seatY = C + R * Math.Sin(a) - 2.75;
            System.Windows.Controls.Canvas.SetLeft(dot, seatX);
            System.Windows.Controls.Canvas.SetTop(dot, seatY);
            // rasterize once, then the rotation moves the bitmap (crisp at any DPI)
            dot.CacheMode = new BitmapCache { RenderAtScale = VisualTreeHelper.GetDpi(dot).DpiScaleX };
            dot.RenderTransform = new RotateTransform(225, C - seatX, C - seatY);
            LoadDots.Children.Add(dot);

            var sb = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(-k * Tau), // explicit value — never null (dead clock)
            };
            Timeline.SetDesiredFrameRate(sb, 240); // pace at the display's rate, not 60

            // angle keyframes = the CSS rotate stops; easing belongs to the segment ending here
            var ang = new DoubleAnimationUsingKeyFrames();
            void Key(double sec, double deg, KeySpline ease) =>
                ang.KeyFrames.Add(new SplineDoubleKeyFrame(deg, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(sec)))
                {
                    KeySpline = ease,
                });
            Key(0, 225, linear);
            Key(0.336, 345, easeOut);      // 7%  225→345deg  ease-out (fast open)
            Key(1.440, 455, linear);       // 30% 345→455deg  linear (slow cruise)
            Key(1.872, 690, easeInOut);    // 39% 455→690deg  ease-in-out (mid burst)
            Key(3.360, 815, linear);       // 70% 690→815deg  linear (slow cruise)
            Key(3.600, 945, easeOut);      // 75% 815→945deg  ease-out (closing burst)
            Key(4.800, 945, linear);       // 75-100%: hidden hold at the lap end
            Storyboard.SetTarget(ang, dot);
            Storyboard.SetTargetProperty(ang, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
            sb.Children.Add(ang);

            // CSS opacity: 1 until 70%, out by 76%, hidden to the loop end (pop-in at 0%)
            var fade = new DoubleAnimationUsingKeyFrames();
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.36))));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.648))));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(4.8))));
            Storyboard.SetTarget(fade, dot);
            Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));
            sb.Children.Add(fade);

            _dotSbs.Add(sb);
            sb.Begin(dot);
        }
    }

    void StopLoadingDots()
    {
        foreach (var sb in _dotSbs) sb.Remove();
        _dotSbs.Clear();
        LoadDots.Children.Clear();
    }

    /// <summary>WPF has no letter-spacing — track text out with thin spaces (呼吸感).</summary>
    static string Spread(string s, char sp) =>
        s.Length < 2 ? s : string.Join(sp.ToString(), s.Select(c => c.ToString()));

    /// <summary>Soft re-materialize after a detection refresh (fires only when a value changed).</summary>
    static void RefreshFade(UIElement el) =>
        el.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0.35, 1, Motion.Normal) { EasingFunction = Motion.DecelerateMax });

    // ---------- log (blue timestamps + conversation names) ----------
    void AddLogEntry(string message, IReadOnlyList<string> blueParts)
    {
        var tb = new TextBlock
        {
            FontFamily = (FontFamily)FindResource("BodyFont"),
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 3),
        };
        // SetResourceReference keeps runs theme-reactive (a static FindResource brush
        // would stay light-theme ink and become invisible after a dark switch).
        Run MakeRun(string text, string key)
        {
            var r = new Run(text);
            r.SetResourceReference(Run.ForegroundProperty, key);
            return r;
        }

        // [HH:mm:ss] in the numeral font (HFSandBeach), per spec
        string stamp = "[" + DateTime.Now.ToString("HH:mm:ss") + "] ";
        var stampRun = new Run(stamp) { FontFamily = (FontFamily)FindResource("DisplayFont") };
        stampRun.SetResourceReference(Run.ForegroundProperty, "LogAccentBrush");
        tb.Inlines.Add(stampRun);

        // paint every occurrence of a blue part (times, conversation names) in blue
        string rest = message;
        while (rest.Length > 0)
        {
            int bestIdx = -1, bestLen = 0;
            foreach (var part in blueParts)
            {
                if (part.Length == 0) continue;
                int i = rest.IndexOf(part, StringComparison.Ordinal);
                if (i >= 0 && (bestIdx < 0 || i < bestIdx)) { bestIdx = i; bestLen = part.Length; }
            }
            if (bestIdx < 0) { tb.Inlines.Add(MakeRun(rest, "TextPrimaryBrush")); break; }
            if (bestIdx > 0) tb.Inlines.Add(MakeRun(rest[..bestIdx], "TextPrimaryBrush"));
            tb.Inlines.Add(MakeRun(rest.Substring(bestIdx, bestLen), "LogAccentBrush"));
            rest = rest[(bestIdx + bestLen)..];
        }

        LogList.Children.Add(tb);
        LogEmptyHint.Visibility = Visibility.Collapsed;
        while (LogList.Children.Count > 200) LogList.Children.RemoveAt(0);
        LogScroll.ScrollToEnd();
    }

    void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        LogList.Children.Clear();
        LogEmptyHint.Visibility = Visibility.Visible;
    }

    // ---------- scan & render ----------
    System.Threading.Tasks.Task ScanAsync()
    {
        // Timer ticks and manual checks share one scan; fast polling never queues more work.
        if (_scanTask is { IsCompleted: false }) return _scanTask;
        return _scanTask = ScanAndRescheduleAsync();
    }

    async System.Threading.Tasks.Task ScanAndRescheduleAsync()
    {
        _scanTimer.Stop();
        try { await ScanCoreAsync(); }
        finally
        {
            _scanTask = null;
            // Re-read the state: Run/Stop or a setting may have changed during the scan.
            if (IsLoaded && !_forceClose) UpdateScanInterval();
        }
    }

    async System.Threading.Tasks.Task ScanCoreAsync()
    {
        _lastDetectAt = DateTime.Now; // 用户：离线也照常显示检测时间（结果才是「未连接」）
        var snap = await System.Threading.Tasks.Task.Run(CodexService.ReadQuota);
        _snap = snap;
        Render(); // land window/quota state now — the account RPC below can cold-start ~15s

        // all conversations (running + idle) + account are cheap reads on the same cadence
        var threads = await System.Threading.Tasks.Task.Run(CodexService.AllThreads);
        _threads = threads;
        // account 每轮现读不缓存——它是「有没有网」的判据（用户三态口径：拿不到
        // 账号名=连上但没网），缓存会把断网误判成正常
        _account = await System.Threading.Tasks.Task.Run(CodexService.ReadAccount);
        _conn = snap.State == QuotaState.Unknown ? ConnState.Offline
            : _account == null ? ConnState.NoNetwork
            : ConnState.Online;

        RefreshTargetThread();
        if (_mode == RunMode.Manual) FillDefaultSendTime();
        Render();

        // A timed retry can recover the offline connect page after its initial handshake timed out.
        if (ConnectPage.Visibility == Visibility.Visible && !_connectTimer.IsEnabled && snap.State != QuotaState.Unknown)
        {
            _prefetched = snap;
            EnterMain();
        }

        if (!_armed || _sending || DateTime.Now < _retryAfter) return;

        if (_mode != RunMode.Auto) return;

        if (ShouldSendOnRecovery(snap))
        {
            AddLogEntry(Loc.T("log.send", _targetThreadName), new[] { _targetThreadName });
            await FireAsync();
        }
        else if (snap.State != QuotaState.Unknown && !snap.IsExhausted(DateTime.Now) && !_sawExhausted && !_firedThisCycle)
        {
            // armed after the reset already happened: Codex stopped on a limit and quota is back
            int revision = _runRevision;
            if (await System.Threading.Tasks.Task.Run(CodexService.LastTurnIsLimitError) && _armed && revision == _runRevision)
            {
                _sawExhausted = true;
                AddLogEntry(Loc.T("log.send", _targetThreadName), new[] { _targetThreadName });
                await FireAsync();
            }
        }
    }

    bool ShouldSendOnRecovery(QuotaSnapshot snap)
    {
        if (snap.State == QuotaState.Unknown) return false;
        bool exhausted = snap.IsExhausted(DateTime.Now);
        if (_firedThisCycle)
        {
            if (!exhausted)
            {
                _sawExhausted = false;
                _target = null;
                return false;
            }
            // The same stale banner is not a new exhaustion cycle.
            if (_sawExhausted && !(snap.ResetAt is { } next && _target is { } previous && next > previous)) return false;
            _firedThisCycle = false;
            _sawExhausted = false;
        }
        if (!exhausted) return _sawExhausted;
        if (!_sawExhausted)
        {
            _sawExhausted = true;
            _exhaustedAt = DateTime.Now;
        }
        _target = snap.ResetAt ?? _exhaustedAt?.AddHours(5);
        return _target is { } t && DateTime.Now > t.AddMinutes(2);
    }

    void RefreshTargetThread()
    {
        // Preserve only the exact conversation the user chose; never supply a default.
        string? keep = _targetThreadId;
        // 用户：没有对话就整行收掉，不摆空下拉卡；有内容才正常显示
        ConvoRow.Visibility = _threads.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ConvoPick.ItemsSource = _threads;
        int idx = keep == null ? -1 : _threads.FindIndex(t => t.Id == keep);
        ConvoPick.SelectedIndex = idx;
        UpdateCommandState();
    }

    void ConvoPick_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCommandState();
    }

    void Render()
    {
        double avail = 100 - (_snap.UsedPercent ?? 0);
        if (_snap.UsedPercent is null) avail = _snap.State == QuotaState.Ready ? 100 : 0;

        bool offline = _snap.UsedPercent is null && _snap.State == QuotaState.Unknown;
        bool noNet = _conn == ConnState.NoNetwork;   // 连上但没网：额度不可信（用户口径）
        // offline: hero speaks "未连接"（红 36px）; noNet: "—"（不给假数字）; online: art numerals
        string pctText = offline ? Loc.T("quota.offline") : noNet ? "—" : $"{avail:0}";
        // 动画三口径（用户 10-31）：随机揭示只留「进入首页即见数字」；首页检测首现数字=顺序从 0 滚入；
        // 数值变更=顺序滚动原位到目标；同值渲染零动画（运行/停止不得动额度显示和进度条）
        bool showingNumber = !offline && !noNet;
        bool wasUnknown = _displayUnknown;
        _displayUnknown = !showingNumber;
        if (showingNumber && (_entryRevealPending || wasUnknown || avail != _lastPct))
        {
            if (_entryRevealPending) { _entryRevealPending = false; PlayQuotaReveal(avail, _revealDelayMs); _revealDelayMs = 0; }
            else if (wasUnknown) PlayQuotaUpdate(0, avail, fromZero: true);
            else PlayQuotaUpdate(CurrentQuotaPercent, avail, fromZero: false);
        }
        else if (!showingNumber)
        {
            StopQuotaDigits();
            SetQuotaBarImmediately(0);
            if (QuotaPct.Text != pctText) { QuotaPct.Text = pctText; RefreshFade(QuotaPct); }
        }
        QuotaPct.FontFamily = offline ? OfflineHeroFont : (FontFamily)FindResource("DisplayFont");
        QuotaPct.FontSize = offline ? 36 : 72;
        QuotaPct.FontWeight = offline ? FontWeights.Bold : FontWeights.Normal;
        QuotaLabel.Visibility = offline ? Visibility.Collapsed : Visibility.Visible;
        QuotaSign.Visibility = showingNumber ? Visibility.Visible : Visibility.Collapsed;
        // 检测时间 = 本次检测动作的时刻（离线也显示，用户口径），不是数据观测旧戳
        DateTime at = _lastDetectAt != default ? _lastDetectAt : _snap.ObservedAt;
        bool noTime = at == default;
        string timeText = noTime ? "--:--" : at.ToString("HH:mm");
        if (QuotaTime.Text != timeText) { QuotaTime.Text = timeText; RefreshFade(QuotaTime); }
        QuotaTime.SetResourceReference(TextBlock.ForegroundProperty, noTime ? "TextTertiaryBrush" : "TextPrimaryBrush");
        QuotaTime.ToolTip = Loc.T("quota.detectTime");
        _lastPct = avail;
        if (!_revealing && showingNumber) UpdateQuotaBar();

        // status rows
        bool online = _snap.State != QuotaState.Unknown;
        WindowDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, online ? "OkBrush" : "ErrBrush");
        ValWindow.Text = online ? Loc.T("status.online") : Loc.T("status.offline");
        ValAccount.Text = _account?.Email ?? Loc.T("status.none");
        ValConvos.Text = _threads.FindAll(t => t.Running).Count.ToString(CultureInfo.InvariantCulture);
        ValQuota.Text = _snap.State switch
        {
            QuotaState.Ready => Loc.T("status.ok"),
            QuotaState.Limited or QuotaState.LimitedNoTime => Loc.T("status.exhausted"),
            _ => Loc.T("status.none"),
        };
        ValReset.Text = _snap.ResetAt.HasValue
            ? _snap.ResetAt.Value.ToString("MM月dd日 HH:mm", CultureInfo.InvariantCulture)
            : Loc.T("status.none");
        // 一周额度：secondary 窗余量（与主卡「额度=余量」同语义），正常字体显示（用户口径）
        ValSource.Text = _snap.WeeklyPercent is { } wp ? $"{Math.Round(100 - wp, 1):0}%" : "-- --";

        // unified result slot left of the buttons (用户：结果统一显示在按钮左侧)
        if (_armed)
        {
            ShowBtnStatus(
                _mode == RunMode.Auto && _firedThisCycle && !_sending ? Loc.T("cmd.waitingNextCycle") :
                (_mode == RunMode.Manual ? _manualSendAt : _target) is { } est
                    ? Loc.T("cmd.running", est.ToString("HH:mm"))
                    : Loc.T("cmd.runningNoTime"),
                "ErrBrush");
        }
    }

    /// <summary>Detection/run outcome text at the LEFT of the action buttons.</summary>
    void ShowBtnStatus(string text, string? brushKey = null)
    {
        BtnStatus.Text = text;
        BtnStatus.SetResourceReference(TextBlock.ForegroundProperty, brushKey ?? "TextSecondaryBrush");
        if (BtnStatus.Visibility != Visibility.Visible)
        {
            BtnStatus.Visibility = Visibility.Visible;
            Motion.SlideFadeIn(BtnStatus, Motion.Fast, 4);
        }
    }

    double CurrentQuotaPercent => QuotaTrack.ActualWidth > 0
        ? Math.Round(Math.Clamp(QuotaFill.Width / QuotaTrack.ActualWidth * 100, 0, 100), 6) : Math.Max(0, _lastPct);

    double QuotaBarWidth(double percent) => percent <= 0.5 ? 0
        : Math.Min(QuotaTrack.ActualWidth, Math.Max(10, Math.Clamp(percent, 0, 100) / 100 * QuotaTrack.ActualWidth));

    void UpdateQuotaColor(double percent, bool animate, bool force = false)
    {
        string key = percent switch { > 50 => "QuotaBlueBrush", < 10 => "QuotaRedBrush", _ => "QuotaYellowBrush" };
        if (!force && key == _quotaInkKey) return;
        _quotaInkKey = key;
        Color from = _quotaInk.Color;
        Color target = ((SolidColorBrush)FindResource(key)).Color;
        _quotaInk.BeginAnimation(SolidColorBrush.ColorProperty, null);
        _quotaInk.Color = target;
        if (animate && SystemParameters.ClientAreaAnimation && from != target)
            _quotaInk.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(from, target, TimeSpan.FromMilliseconds(180)) { EasingFunction = Motion.SoftOut });
    }

    void QuotaFill_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateQuotaColor(CurrentQuotaPercent, animate: true);

    void StopQuotaDigits()
    {
        _quotaDigitTimer?.Stop();
        _quotaDigitTimer = null;
        _revealing = false;
    }

    void ResetQuotaTrail()
    {
        QuotaTrail.BeginAnimation(WidthProperty, null);
        QuotaTrail.Width = 0;
        QuotaTrail.Visibility = Visibility.Collapsed;
    }

    void SetQuotaBarImmediately(double percent)
    {
        QuotaFill.BeginAnimation(WidthProperty, null);
        QuotaFill.Width = QuotaBarWidth(percent);
        ResetQuotaTrail();
        UpdateQuotaColor(percent, animate: false, force: true);
    }

    void StartQuotaTrail(double from, double to, double mainDuration, bool isLoss)
    {
        if (!isLoss || to >= from || !SystemParameters.ClientAreaAnimation) { ResetQuotaTrail(); return; }
        double start = Math.Max(from, QuotaTrail.Width);
        QuotaTrail.BeginAnimation(WidthProperty, null);
        QuotaTrail.Width = start;
        QuotaTrail.Visibility = Visibility.Visible;
        // The loss echo follows a gentler curve than the main bar's sharp QuartOut.
        QuotaTrail.BeginAnimation(WidthProperty,
            new DoubleAnimation(start, to, TimeSpan.FromMilliseconds(Math.Max(650, mainDuration * 1.25)))
            {
                BeginTime = TimeSpan.FromMilliseconds(240),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
    }

    void UpdateQuotaBar()
    {
        if (_revealing) return; // reveal owns the bar; SizeChanged must not stomp it
        if (_displayUnknown || _lastPct < 0 || QuotaTrack.ActualWidth <= 0) return;
        double target = QuotaBarWidth(_lastPct);
        if (Math.Abs(QuotaFill.Width - target) < 0.01) return;
        var anim = new DoubleAnimation(target, SystemParameters.ClientAreaAnimation ? Motion.Normal : new Duration(TimeSpan.Zero))
            { EasingFunction = Motion.DecelerateMax, From = QuotaFill.Width };
        QuotaFill.BeginAnimation(WidthProperty, anim);
    }

    /// <summary>Quota reveal: digits scramble 0-9 and settle on the value (非线性渐停),
    /// the bar retracts from the RIGHT edge down to width — both land together.
    /// Plays on 未连接→额度 transitions (auto-entry from connect, or a later detection).
    /// startDelay lets the slide-uncover catch the churn (user: 三动画同播可见).</summary>
    void PlayQuotaReveal(double avail, int startDelayMs = 0)
    {
        StopQuotaDigits();
        ResetQuotaTrail();
        if (!SystemParameters.ClientAreaAnimation)
        {
            SetQuotaBarImmediately(avail);
            QuotaPct.Text = $"{avail:0}";
            return;
        }
        _revealing = true;
        string target = $"{avail:0}";
        const double durMs = 1100;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rnd = new Random();
        var timer = _quotaDigitTimer = new DispatcherTimer();
        // 数字与条同刻落定的唯一真相：条动画的 Completed（动画时钟精确）——
        // tick 只管中途乱码滚动，谁先停不再赌拍相位
        void Settle()
        {
            if (!ReferenceEquals(_quotaDigitTimer, timer)) return;
            QuotaPct.Text = target;
            UpdateQuotaColor(avail, animate: true);
            StopQuotaDigits();
            UpdateQuotaBar();
        }
        // bar (用户: 方向跟数值走)：>50% 从左往右 0→目标（数涨条涨）；<50% 从右往左
        // 满格→目标（数跌条收）。≤0.5 按 0 收（与 UpdateQuotaBar 同阈值，不留 10px 药丸）。
        if (QuotaTrack.ActualWidth > 0)
        {
            double tw = QuotaBarWidth(avail);
            double from = avail >= 50 ? 0 : QuotaTrack.ActualWidth;
            QuotaFill.Visibility = Visibility.Visible; // 收到 0 宽自然隐，过程可见
            var bar = new DoubleAnimation(from, tw,
                new Duration(TimeSpan.FromMilliseconds(durMs)))
            {
                BeginTime = TimeSpan.FromMilliseconds(startDelayMs),
                EasingFunction = Motion.QuartOut, // 速率差加大：起步冲刺、长尾缓停
            };
            Timeline.SetDesiredFrameRate(bar, 240);
            bar.Completed += (_, _) => Settle();
            QuotaFill.BeginAnimation(WidthProperty, bar);
        }
        // digits: 快滚→急缓（节拍 30→250ms，速率差拉满），前位渐进锁定、末位
        // 一路乱码到条落定才定格；条缺席时（宽度 0）退回 u=1 自落
        timer.Tick += (_, _) =>
        {
            double el = sw.ElapsedMilliseconds - startDelayMs;
            if (el < 0) return;
            double u = Math.Min(1, el / durMs);
            if (u >= 1) { Settle(); return; }
            UpdateQuotaColor(CurrentQuotaPercent, animate: true);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < target.Length; i++)
            {
                bool last = i == target.Length - 1;
                double lockAt = 0.5 + 0.35 * (i + 1) / target.Length; // 前位渐锁，末位不锁
                sb.Append(!last && u >= lockAt ? target[i] : (char)('0' + rnd.Next(10)));
            }
            QuotaPct.Text = sb.ToString();
            timer.Interval = TimeSpan.FromMilliseconds(30 + 220 * u); // 快→慢，速率差 8×
        };
        timer.Interval = TimeSpan.FromMilliseconds(30);
        timer.Start();
    }

    /// <summary>检测更新额度（用户 10-31 口径）：数字按顺序（非随机）从起始值滚到目标，
    /// 位数变化字宽平滑收放=后面内容自动往前缩进；进度条从原有位置（首现数字=同入口
    /// 端点规则）非线性先快后慢到目标；条 Completed 同刻定格（与揭示同纪律）。</summary>
    void PlayQuotaUpdate(double from, double to, bool fromZero)
    {
        StopQuotaDigits();
        if (!SystemParameters.ClientAreaAnimation)
        {
            SetQuotaBarImmediately(to);
            QuotaPct.Text = $"{to:0}";
            return;
        }
        _revealing = true;
        double durMs = Math.Min(1200, Math.Max(320, 200 + Math.Abs(to - from) * 60));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var timer = _quotaDigitTimer = new DispatcherTimer();
        int lastShown = int.MinValue;
        void Settle()
        {
            if (!ReferenceEquals(_quotaDigitTimer, timer)) return;
            SetQuotaTextSmooth($"{to:0}", force: true);
            UpdateQuotaColor(to, animate: true);
            StopQuotaDigits();
            UpdateQuotaBar();
        }
        if (QuotaTrack.ActualWidth > 0)
        {
            double tw = QuotaBarWidth(to);
            double cw = fromZero ? (to >= 50 ? 0 : QuotaTrack.ActualWidth) : QuotaFill.Width;
            StartQuotaTrail(cw, tw, durMs, !fromZero && to < _lastPct);
            QuotaFill.Visibility = Visibility.Visible;
            var bar = new DoubleAnimation(cw, tw, new Duration(TimeSpan.FromMilliseconds(durMs)))
            { EasingFunction = Motion.QuartOut };   // 先快后慢，与揭示同族
            Timeline.SetDesiredFrameRate(bar, 240);
            bar.Completed += (_, _) => Settle();
            QuotaFill.BeginAnimation(WidthProperty, bar);
        }
        timer.Interval = TimeSpan.FromMilliseconds(20);
        timer.Tick += (_, _) =>
        {
            double u = Math.Min(1, sw.ElapsedMilliseconds / durMs);
            if (u >= 1) { Settle(); return; }
            // Pixel rounding can suppress SizeChanged for a small threshold crossing.
            UpdateQuotaColor(CurrentQuotaPercent, animate: true);
            int v = (int)Math.Round(from + (to - from) * Motion.QuartOut.Ease(u));
            if (v != lastShown) { SetQuotaTextSmooth(v.ToString(CultureInfo.InvariantCulture)); lastShown = v; }
        };
        timer.Start();
    }

    /// <summary>落数字；位数变化时让宽度变化平滑：% 与标签自动缩进不跳位。</summary>
    void SetQuotaTextSmooth(string text, bool force = false)
    {
        if (!force && QuotaPct.Text == text) return;
        bool countChanged = QuotaPct.Text.Length != text.Length;   // 只有位数真变才动布局
        double oldW = double.IsNaN(QuotaPct.Width) ? QuotaPct.ActualWidth : QuotaPct.Width;
        QuotaPct.Text = text;
        if (!countChanged) return;
        QuotaPct.UpdateLayout();
        double newW = QuotaPct.ActualWidth;
        double delta = newW - oldW;
        if (Math.Abs(delta) < 0.5) return;
        QuotaPct.BeginAnimation(WidthProperty, null);
        if (delta < 0)
        {
            // 收（100→99）：钉旧宽平滑收到新宽，兄弟元素随宽度渐进前移
            var wa = new DoubleAnimation { From = oldW, To = newW, Duration = TimeSpan.FromMilliseconds(180), EasingFunction = Motion.SettleOut };
            wa.Completed += (_, _) => { QuotaPct.BeginAnimation(WidthProperty, null); QuotaPct.Width = double.NaN; };
            QuotaPct.Width = oldW;
            QuotaPct.BeginAnimation(WidthProperty, wa);
        }
        else
        {
            // 放（9→10）：文字立即完整可见，负右缘把兄弟元素平滑推开
            var ma = new ThicknessAnimation { From = new Thickness(0, 0, -delta, 0), To = new Thickness(0), Duration = TimeSpan.FromMilliseconds(180), EasingFunction = Motion.SettleOut };
            ma.Completed += (_, _) =>
            {
                QuotaPct.BeginAnimation(MarginProperty, null);
                QuotaPct.BeginAnimation(WidthProperty, null);
                QuotaPct.Width = double.NaN; QuotaPct.Margin = new Thickness(0);
            };
            QuotaPct.Width = newW;
            QuotaPct.Margin = new Thickness(0, 0, -delta, 0);
            QuotaPct.BeginAnimation(MarginProperty, ma);
        }
    }

    void QuotaTrack_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateQuotaBar();

    // ---------- splash / connect flow ----------
    /// <summary>Splash entrance — ~1s total, 前快后慢 (Fluent decelerate 0.1,0.9,0.2,1):
    /// title materializes and the key lifts into place fast-then-soft, no drag, no tail.
    /// Title/key carry BitmapCaches during the fade (live ClearType text drops to
    /// grayscale mid-fade and pops back = the flicker; cached bitmap fades clean).</summary>
    void SplashEnter()
    {
        foreach (var el in new System.Windows.UIElement[] { SplashTitleShadow, SplashTitle })
        {
            el.CacheMode = new BitmapCache { RenderAtScale = VisualTreeHelper.GetDpi(el).DpiScaleX };
            var fade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(900)))
            {
                BeginTime = TimeSpan.FromMilliseconds(100),
                EasingFunction = Motion.DecelerateMax,
            };
            Timeline.SetDesiredFrameRate(fade, 240);
            el.BeginAnimation(UIElement.OpacityProperty, fade);
        }
        var rise = new TranslateTransform(0, 22);
        BtnStart.RenderTransform = rise;
        BtnStart.CacheMode = new BitmapCache { RenderAtScale = VisualTreeHelper.GetDpi(BtnStart).DpiScaleX };
        // 用户口径：淡入要「慢慢出现在视野」——先快后慢但起步温和（SoftOut
        // ease-out quad），不用 DecelerateMax（起步即 90% 速度=炸现的真凶）
        var btnFade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(800)))
        {
            BeginTime = TimeSpan.FromMilliseconds(150),
            EasingFunction = Motion.SoftOut,
        };
        Timeline.SetDesiredFrameRate(btnFade, 240);
        btnFade.Completed += (_, _) =>
        {
            // BitmapCache keeps the fade clean but clips the keycap shadows to the
            // button bounds — drop it once the entrance is done so the shadows live
            BtnStart.CacheMode = null;
        };
        BtnStart.BeginAnimation(UIElement.OpacityProperty, btnFade);
        var up = new DoubleAnimation(22, 0, new Duration(TimeSpan.FromMilliseconds(850)))
        {
            BeginTime = TimeSpan.FromMilliseconds(150),
            EasingFunction = Motion.SoftOut,
        };
        Timeline.SetDesiredFrameRate(up, 240);
        rise.BeginAnimation(TranslateTransform.YProperty, up);
    }

    void BtnStart_Click(object sender, RoutedEventArgs e) => BeginConnect();

    void BtnLink_Click(object sender, RoutedEventArgs e) => BeginConnect();

    /// <summary>Skip the handshake and enter the main page in offline state.</summary>
    void SkipLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _connectTimer.Stop();
        EnterMain();
    }

    void BeginConnect()
    {
        if (_startupTransitionActive) return;
        _connectOpenedAt = DateTime.Now;
        ConnectTitle.Text = Spread(Loc.T("connect.title"), ' ');
        ConnectMsg.Text = Spread(Loc.T("connect.bottom"), ' ');
        StartLoadingDots();
        BtnLink.Visibility = Visibility.Collapsed;
        _dotTick = 0;

        if (SplashPage.Visibility == Visibility.Visible)
        {
            BeginStartupTransition(SystemParameters.ClientAreaAnimation);
        }
        else
        {
            ConnectPage.Visibility = Visibility.Visible;
            TitleIcon.Visibility = TitleName.Visibility = Visibility.Visible;
            LoadDots.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350)) { EasingFunction = Motion.DecelerateMax });
        }

        _connectDeadline = DateTime.Now.AddSeconds(30);
        _foundPending = false;
        _prefetched = null;
        _connectTimer.Start();
    }

    void ConnectTick(object? sender, EventArgs e)
    {
        _dotTick++;
        // Probe every 1s from the start — the 5s floor gates ENTRY only, not detection
        // (用户：5 秒加载期内把额度检测做完，进首页直接见数字). FindWindow is a cheap
        // sweep; ReadQuota (UIA+RPC) runs off-thread as the prefetch.
        if (_dotTick % 2 == 0 && !_probing && _prefetched == null)
        {
            _probing = true;
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                // 探测链必须永不死锁：found 立刻回传，额度读失败/异常都只丢弃结果
                bool found = CodexService.IsCodexRunning();
                Dispatcher.BeginInvoke(() =>
                {
                    if (found && _connectTimer.IsEnabled) { _foundPending = true; TryEnterFromConnect(); }
                });
                QuotaSnapshot? snap = null;
                try { if (found) snap = CodexService.ReadQuota(); } catch { }
                Dispatcher.BeginInvoke(() =>
                {
                    _probing = false;
                    if (snap != null && _connectTimer.IsEnabled)
                    {
                        _prefetched = snap;
                        TryEnterFromConnect();
                    }
                });
            });
        }
        TryEnterFromConnect();
        if (DateTime.Now >= _connectDeadline)
        {
            _connectTimer.Stop();
            StopLoadingDots();
            ConnectMsg.Text = Spread(Loc.T("connect.timeout"), ' ');
            BtnLink.Visibility = Visibility.Visible;
            Motion.SlideFadeIn(BtnLink, Motion.Fast, 6);
        }
    }

    /// <summary>Entry gate: Codex found + the 5s floor fully spent. Quota prefetch is
    /// best-effort — waits up to 8s past the floor, then enters anyway and lets the
    /// first successful scan play the reveal (never wedge the connect page).</summary>
    void TryEnterFromConnect()
    {
        if (!_foundPending || !_connectTimer.IsEnabled) return;
        var waited = DateTime.Now - _connectOpenedAt;
        if (waited < TimeSpan.FromSeconds(5)) return;
        if (_prefetched == null && waited < TimeSpan.FromSeconds(13)) return;
        _connectTimer.Stop();
        EnterMain();
    }

    void EnterMain()
    {
        FinishStartupTransition(preserveContent: true);
        _materialMainEntered = true;
        UpdateMaterialCoverage();
        LayoutRoot.UpdateLayout(); // Glass startup hides the dashboard until now; quota animation needs its measured width.
        StopLoadingDots();
        // 连接页预检到的额度直接落座：进首页即见数字（不再闪「未连接」），额度揭示
        // （数字滚动+进度条从右收回）与上滑出场同时起跑。跳过入场则先渲染「未连接」，
        // 后续检测成功时照常播揭示（用户口径）
        if (_prefetched is { } snap)
        {
            _prefetched = null;
            _snap = snap;
            _lastDetectAt = snap.ObservedAt != default ? snap.ObservedAt : DateTime.Now;
            // 上滑加速离场（560ms）末段才露出英雄区——揭示延迟 500ms 与之对齐，
            // 数字滚动+进度条收回随页面揭开同框可见（用户：三动画同播）
            _entryRevealPending = true;
            _revealDelayMs = 500;
            _ = System.Threading.Tasks.Task.Run(CodexService.AllThreads).ContinueWith(t =>
                Dispatcher.BeginInvoke(() =>
                {
                    _threads = t.Result;
                    RefreshTargetThread();
                    Render();
                }));
        }
        Render();
        // 用户口径：连接页整体上滑出窗（先慢后快加速离场），首页本来就压
        // 在下方（低 Z 序）随遮挡让出直接露出——跳过/自动连接两条路都走这里
        ConnectPage.BeginAnimation(OpacityProperty, null); // 短路没走完的淡入
        ConnectPage.Opacity = 1;
        if (ThemeManager.TransparencyEnabled)
            ConnectPage.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = Motion.SoftOut });
        var up = new Duration(TimeSpan.FromMilliseconds(560));
        var t = new TranslateTransform();
        ConnectPage.RenderTransform = t;
        var slide = new DoubleAnimation(0, -ActualHeight, up) { EasingFunction = Motion.AccelerateOff };
        Timeline.SetDesiredFrameRate(slide, 240);
        slide.Completed += (_, _) =>
        {
            ConnectPage.Visibility = Visibility.Collapsed;
            ConnectPage.RenderTransform = new TranslateTransform(); // 复位供下次进入
        };
        t.BeginAnimation(TranslateTransform.YProperty, slide);
        _ = StartupScanAsync();
    }

    async System.Threading.Tasks.Task StartupScanAsync()
    {
        // connectivity probe: milliseconds, paints the real answer before the slow chain
        bool alive = CodexService.IsCodexRunning();
        WindowDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, alive ? "OkBrush" : "ErrBrush");
        ValWindow.Text = Loc.T(alive ? "status.online" : "status.offline");

        await ScanAsync();
        AddLogEntry(Loc.T("log.enter"), Array.Empty<string>());
        UpdateScanInterval();
    }

    async void BtnRecheck_Click(object sender, RoutedEventArgs e)
    {
        // 用户：检测=连接并检测，一次就好——不轮询不连环，结果落在按钮左侧
        if (_scanning) return;
        _scanning = true;
        AddLogEntry(Loc.T("log.recheck"), Array.Empty<string>());

        // busy feedback: the key dims and breathes until the scan lands
        BtnRecheck.IsEnabled = false;
        BtnRecheck.Content = Loc.T("cmd.checking");
        var pulse = new DoubleAnimation(1, 0.55, new Duration(TimeSpan.FromMilliseconds(320)))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        BtnRecheck.BeginAnimation(OpacityProperty, pulse);

        await ScanAsync();

        BtnRecheck.BeginAnimation(OpacityProperty, null);
        BtnRecheck.Opacity = 1;
        BtnRecheck.Content = Loc.T("cmd.recheck");
        BtnRecheck.IsEnabled = true;
        _scanning = false;

        // 三态反馈（用户口径）：③没连上=请启动Codex后重新尝试；②连上没网=无法获取额度请检查网络；①正常=已连接额度已更新
        ShowBtnStatus(_conn switch
        {
            ConnState.Online => Loc.T("cmd.detectOk"),
            ConnState.NoNetwork => Loc.T("cmd.detectNet"),
            _ => Loc.T("connect.timeout"),
        });
    }

    // ---------- modes ----------
    void Navigation_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton option && Enum.TryParse((string)option.Tag, out ViewMode view))
            SwitchView(view);
    }

    void SwitchView(ViewMode view)
    {
        if (_view == view) return;
        bool runModeSwitch = (_view is ViewMode.Auto or ViewMode.Manual) && (view is ViewMode.Auto or ViewMode.Manual);
        bool crossGroup = (_view is ViewMode.Auto or ViewMode.Manual) != (view is ViewMode.Auto or ViewMode.Manual);
        _view = view;
        int revision = ++_viewRevision;
        if (view is ViewMode.Auto or ViewMode.Manual)
        {
            var mode = view == ViewMode.Auto ? RunMode.Auto : RunMode.Manual;
            if (_mode != mode)
            {
                if (_armed) Disarm(Loc.T("log.stopped"));
                _mode = mode;
            }
        }
        if (view == ViewMode.Settings) LoadSettingsSelection();
        UpdateNavigation(animate: true, durationMs: crossGroup ? 460 : 380);
        ConvoPick.IsDropDownOpen = false;
        if (runModeSwitch && RunPage.Visibility == Visibility.Visible && ContentHost.IsHitTestVisible)
        {
            UpdateRunModeControls(animate: true);
            return;
        }
        if (ContentHost.IsKeyboardFocusWithin) System.Windows.Input.Keyboard.ClearFocus();
        ContentHost.IsHitTestVisible = false;

        void Reveal()
        {
            if (revision != _viewRevision) return;
            RunPage.Visibility = view is ViewMode.Auto or ViewMode.Manual ? Visibility.Visible : Visibility.Collapsed;
            SettingsPage.Visibility = view == ViewMode.Settings ? Visibility.Visible : Visibility.Collapsed;
            LogPage.Visibility = view == ViewMode.Log ? Visibility.Visible : Visibility.Collapsed;
            if (RunPage.Visibility == Visibility.Visible)
                UpdateRunModeControls(animate: false);
            if (view == ViewMode.Log) LogScroll.ScrollToEnd();
            ContentHost.BeginAnimation(OpacityProperty, null);
            ContentHost.Opacity = 1;
            ContentHost.IsHitTestVisible = true;
            if (SystemParameters.ClientAreaAnimation)
                ContentHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(crossGroup ? 280 : 260))
                    { EasingFunction = Motion.SoftOut });
        }

        if (!SystemParameters.ClientAreaAnimation)
        {
            Reveal();
            return;
        }
        // Swap only at zero opacity so card edges and text never double up.
        var fadeOut = new DoubleAnimation(ContentHost.Opacity, 0, TimeSpan.FromMilliseconds(crossGroup ? 160 : 140))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        fadeOut.Completed += (_, _) => Reveal();
        ContentHost.BeginAnimation(OpacityProperty, fadeOut);
    }

    void UpdateRunModeControls(bool animate)
    {
        int revision = ++_runModeRevision;
        animate &= SystemParameters.ClientAreaAnimation;
        if (_mode == RunMode.Manual) FillDefaultSendTime();
        Fade(SendTimePanel, _mode == RunMode.Manual);
        Fade(LoopPanel, _mode == RunMode.Auto);

        void Fade(FrameworkElement element, bool visible)
        {
            double from = element.Visibility == Visibility.Visible ? element.Opacity : 0;
            element.BeginAnimation(OpacityProperty, null);
            element.IsHitTestVisible = visible;
            if (!visible && element.IsKeyboardFocusWithin) System.Windows.Input.Keyboard.ClearFocus();
            void Finish()
            {
                if (revision != _runModeRevision) return;
                element.BeginAnimation(OpacityProperty, null);
                element.Opacity = 1;
                element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
            if (!animate || (!visible && from == 0)) { Finish(); return; }
            element.Opacity = from;
            element.Visibility = Visibility.Visible;
            var fade = new DoubleAnimation(from, visible ? 1 : 0, TimeSpan.FromMilliseconds(visible ? 220 : 140))
                { EasingFunction = visible ? Motion.SoftOut : new CubicEase { EasingMode = EasingMode.EaseIn } };
            fade.Completed += (_, _) => Finish();
            element.BeginAnimation(OpacityProperty, fade);
        }
    }

    void UpdateNavigation(bool animate, int durationMs = 380)
    {
        animate &= SystemParameters.ClientAreaAnimation;
        _updatingNavigation = true;
        foreach (var button in _modeButtons) button.IsChecked = (string)button.Tag == _view.ToString();
        TabRow.UpdateLayout();
        _updatingNavigation = false;
        UpdateModeTrackGeometry();
        MoveModePill(animate, durationMs);
        _settingsMorph.MorphTo(_view == ViewMode.Settings ? MorphIcons.Settings2 : MorphIcons.Settings, animate);
        _logMorph.MorphTo(_view == ViewMode.Log ? MorphIcons.FileText : MorphIcons.ScrollText, animate);
    }

    void BtnGithub_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => SetGithubHover(true);
    void BtnGithub_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) => SetGithubHover(false);

    void SetGithubHover(bool hovered)
    {
        BtnGithub.ApplyTemplate();
        var glyph = (FrameworkElement)BtnGithub.Template.FindName("GithubGlyph", BtnGithub);
        var transforms = (TransformGroup)glyph.RenderTransform;
        if (transforms.IsFrozen) glyph.RenderTransform = transforms = transforms.Clone();
        bool animate = BtnGithub.IsLoaded && SystemParameters.ClientAreaAnimation;
        var duration = TimeSpan.FromMilliseconds(animate ? hovered ? 240 : 200 : 0);
        DoubleAnimation To(double value) => new(value, duration) { EasingFunction = Motion.SettleOut };
        var scale = (ScaleTransform)transforms.Children[0];
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, To(hovered && animate ? 1.08 : 1));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, To(hovered && animate ? 1.08 : 1));
        transforms.Children[1].BeginAnimation(RotateTransform.AngleProperty, To(hovered && animate ? -6 : 0));
        transforms.Children[2].BeginAnimation(TranslateTransform.YProperty, To(hovered && animate ? -2 : 0));
    }

    void BtnGithub_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo((string)BtnGithub.Tag) { UseShellExecute = true }); }
        catch { ShowHint(Loc.T("github.openFailed", (string)BtnGithub.Tag), confirm: null); }
    }

    void ModeTrack_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateModeTrackGeometry();
        if (!_updatingNavigation && _modeSlide == null) MoveModePill(animate: false);
    }

    void UpdateModeTrackGeometry()
    {
        var tracks = new GeometryGroup();
        var clip = new GeometryGroup();
        foreach (var track in new[] { RunModeTrack, ToolModeTrack })
        {
            var bounds = new Rect(track.TranslatePoint(new Point(), TabRow), track.RenderSize);
            if (bounds.Width <= 8 || bounds.Height <= 8) continue;
            tracks.Children.Add(new RectangleGeometry(bounds, 12, 12));
            bounds.Inflate(-4, -4);
            clip.Children.Add(new RectangleGeometry(bounds, 9, 9));
        }
        ModeTrackFace.Data = tracks;
        ModePillLayer.Clip = clip;
    }

    void MoveModePill(bool animate, int durationMs = 380)
    {
        var target = _modeButtons.First(b => (string)b.Tag == _view.ToString());
        if (target.ActualWidth <= 0) return;
        var transform = (TranslateTransform)ModePill.RenderTransform;
        double fromX = transform.X, fromWidth = ModePill.Width;
        double x = target.TranslatePoint(new Point(), TabRow).X;
        animate &= SystemParameters.ClientAreaAnimation;
        _modeSlide = null;
        CompositionTarget.Rendering -= ModePill_Rendering;
        transform.BeginAnimation(TranslateTransform.XProperty, null);
        ModePill.BeginAnimation(WidthProperty, null);
        // Keep the visible starting value until WPF advances the new animation's first frame.
        transform.X = animate ? fromX : x;
        ModePill.Width = animate ? fromWidth : target.ActualWidth;
        if (animate)
        {
            var slide = new DoubleAnimation(fromX, x, TimeSpan.FromMilliseconds(durationMs)) { EasingFunction = Motion.QuartOut };
            _modeSlide = slide;
            slide.Completed += (_, _) =>
            {
                if (ReferenceEquals(_modeSlide, slide)) MoveModePill(animate: false);
            };
            transform.BeginAnimation(TranslateTransform.XProperty, slide);
            ModePill.BeginAnimation(WidthProperty,
                new DoubleAnimation(fromWidth, target.ActualWidth, slide.Duration) { EasingFunction = Motion.QuartOut });
            CompositionTarget.Rendering += ModePill_Rendering;
        }
        UpdateModeInk();
    }

    void ModePill_Rendering(object? sender, EventArgs e) => UpdateModeInk();

    void UpdateModeInk()
    {
        double left = ((TranslateTransform)ModePill.RenderTransform).X;
        var selectedInk = (Brush)FindResource("AccentTextBrush");
        var dark = (Brush)FindResource("TextPrimaryBrush");
        foreach (var button in _modeButtons)
        {
            double start = button.TranslatePoint(new Point(button.Padding.Left, 0), TabRow).X;
            double end = start + button.ActualWidth - button.Padding.Left - button.Padding.Right;
            // Keep partially covered glyphs dark so their uncovered edge stays readable on white.
            bool covered = start >= left - 0.5 && end <= left + ModePill.Width + 0.5;
            bool overlap = end > left && start < left + ModePill.Width;
            string? key = covered ? "AccentTextBrush" : overlap ? "TextPrimaryBrush" : null;
            if (key != null)
            {
                if (!ReferenceEquals(button.Foreground, covered ? selectedInk : dark))
                    button.SetResourceReference(Control.ForegroundProperty, key);
            }
            else if (button.ReadLocalValue(Control.ForegroundProperty) != DependencyProperty.UnsetValue)
                button.ClearValue(Control.ForegroundProperty);
        }
    }

    void FillDefaultSendTime()
    {
        if (_sendTimeTouched) return;
        _manualSendAt = _snap.ResetAt ?? DateTime.Now.AddHours(5);
        BoxSendTime.Text = FormatSendTime(_manualSendAt.Value);
    }

    static string FormatSendTime(DateTime t) =>
        t.Date == DateTime.Today ? t.ToString("HH:mm") : t.ToString("M月d日 HH:mm", CultureInfo.InvariantCulture);

    void SpnUp_Click(object sender, RoutedEventArgs e) => NudgeSendTime(TimeSpan.FromMinutes(5));
    void SpnDown_Click(object sender, RoutedEventArgs e) => NudgeSendTime(TimeSpan.FromMinutes(-5));

    void NudgeSendTime(TimeSpan delta)
    {
        var t = TryParseSendTime() ?? (_manualSendAt ?? DateTime.Now);
        _manualSendAt = t + delta;
        _sendTimeTouched = true;
        BoxSendTime.Text = FormatSendTime(_manualSendAt.Value);
        Render();
    }

    void BoxSendTime_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        if (TryParseSendTime() is { } t)
        {
            _manualSendAt = t;
            _sendTimeTouched = true;
            Render();
        }
    }

    static readonly Regex ManualRe = new(@"^(?:(\d{1,2})月(\d{1,2})日\s*)?(\d{1,2})[:：](\d{2})$", RegexOptions.Compiled);

    DateTime? TryParseSendTime()
    {
        var s = BoxSendTime.Text.Trim();
        if (s.Length == 0) return null;
        var m = ManualRe.Match(s);
        if (!m.Success) return null;
        int hh = int.Parse(m.Groups[3].Value), mm = int.Parse(m.Groups[4].Value);
        if (hh > 23 || mm > 59) return null;
        DateTime day = DateTime.Today;
        if (m.Groups[1].Success)
        {
            try { day = new DateTime(DateTime.Today.Year, int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)); }
            catch { return null; }
        }
        var result = day.AddHours(hh).AddMinutes(mm);
        if (!m.Groups[1].Success && result < DateTime.Now.AddMinutes(-1)) result = result.AddDays(1);
        return result;
    }

    // ---------- attachments ----------
    void BtnAttach_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Title = Loc.T("cmd.addAttachment"),
        };
        if (dlg.ShowDialog() != true) return;
        foreach (var f in dlg.FileNames)
        {
            if (_attachments.Contains(f)) continue;
            _attachments.Add(f);
            AttachList.Children.Add(BuildChip(f));
        }
    }

    /// <summary>Remove chips only — BtnAttach lives inside AttachList and must survive.</summary>
    void ClearAttachments()
    {
        _attachments.Clear();
        for (int i = AttachList.Children.Count - 1; i >= 0; i--)
            if (AttachList.Children[i] != BtnAttach) AttachList.Children.RemoveAt(i);
    }

    UIElement BuildChip(string path)
    {
        string name = System.IO.Path.GetFileName(path);
        // NeuChip 样式（资源实存）+ Border 承载——曾引用不存在的 "NeuPill" 直接炸穿进程
        var chip = new Border
        {
            Style = (Style)FindResource("NeuChip"),
            Padding = new Thickness(12, 6, 8, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = path,
        };
        chip.SetResourceReference(Border.BackgroundProperty, "TrackBrush");
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var nameText = new TextBlock
        {
            Text = name,
            FontFamily = (FontFamily)FindResource("BodyFont"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 120,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        nameText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        panel.Children.Add(nameText);
        var closeText = new TextBlock
        {
            Text = "",   // close glyph
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe UI"),
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
        };
        closeText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        panel.Children.Add(closeText);
        chip.Child = panel;
        chip.MouseLeftButtonDown += (_, _) =>
        {
            _attachments.Remove(path);
            AttachList.Children.Remove(chip);
        };
        return chip;
    }

    // ---------- arm / disarm ----------
    void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        if (_armed) { Disarm(Loc.T("log.stopped")); return; }
        if (!CommandIsComplete) return;
        // 三态口径：③没连上→提示先连接；②连上没网→警示弹窗，确定=照跑、取消=不跑；①正常→直接运行
        if (!CodexService.IsCodexRunning())
        {
            ShowHint(Loc.T("cmd.needConnect"), confirm: null);
            return;
        }
        if (_conn == ConnState.NoNetwork)
        {
            ShowHint(Loc.T("cmd.noNetWarn"), confirm: Arm);
            return;
        }
        Arm();
    }

    void ShowHint(string body, Action? confirm)
    {
        _hintConfirm = confirm;
        HintBody.Text = body;
        HintCancel.Visibility = confirm == null ? Visibility.Collapsed : Visibility.Visible;
        HintDialog.Visibility = Visibility.Visible;
        HintDialog.BeginAnimation(OpacityProperty, null);
        HintDialog.Opacity = 1;
        HintDialog.RenderTransform = Transform.Identity;
        if (SystemParameters.ClientAreaAnimation) Motion.SlideFadeIn(HintDialog, Motion.Normal, 8);
    }

    Action? _hintConfirm;   // 确定按钮的待执行动作（如「无网仍要运行」），null=纯提示
    void HintOk_Click(object sender, RoutedEventArgs e)
    {
        HintDialog.Visibility = Visibility.Collapsed;
        var a = _hintConfirm; _hintConfirm = null;
        a?.Invoke();
    }

    void HintCancel_Click(object sender, RoutedEventArgs e)
    {
        HintDialog.Visibility = Visibility.Collapsed;
        _hintConfirm = null;
    }

    void Arm()
    {
        if (!CommandIsComplete) return;
        if (_mode == RunMode.Manual)
        {
            _manualSendAt = TryParseSendTime() ?? _snap.ResetAt ?? DateTime.Now.AddHours(5);
            BoxSendTime.Text = FormatSendTime(_manualSendAt.Value);
        }

        _runRevision++;
        _armed = true;
        _firedThisCycle = false;
        _sendAttempts = 0;
        _blockedAttempts = 0;
        _retryAfter = DateTime.MinValue;
        _target = null;
        _exhaustedAt = null;
        _sawExhausted = _mode == RunMode.Auto && _snap.IsExhausted(DateTime.Now);
        if (_sawExhausted)
        {
            _exhaustedAt = DateTime.Now;
            _target = _snap.ResetAt ?? _exhaustedAt?.AddHours(5);
        }

        Native.SetThreadExecutionState(Native.ES_CONTINUOUS | Native.ES_SYSTEM_REQUIRED);

        BtnRun.Content = Loc.T("cmd.stop");
        SetRunFace(danger: true);
        SetCommandLocked(true);    // 运行态锁定续跑指令区（用户 10-31）
        UpdateScanInterval();
        UpdateManualTimer();
        Render();

        // log line per spec: "运行 Codex-Continue，将在 XX:XX 时向 对话名 发送续跑指令。"
        string timeStr = (_mode == RunMode.Manual ? _manualSendAt : _target)?.ToString("HH:mm")
            ?? Loc.T("cmd.onRecover"); // auto mode without a known reset moment
        AddLogEntry(Loc.T("log.armed", timeStr, DisplayName()), new[] { timeStr, DisplayName() });
        // Run itself does not scan; the selected running frequency schedules the next detection.
    }

    void Disarm(string why)
    {
        _runRevision++;
        _armed = false;
        _sending = false;
        _firedThisCycle = false;
        _manualTimer.Stop();
        Native.SetThreadExecutionState(Native.ES_CONTINUOUS);
        BtnRun.Content = Loc.T("cmd.run");
        SetRunFace(danger: false);
        SetCommandLocked(false);
        UpdateScanInterval();
        BtnStatus.Text = "";       // 停止即清按钮左侧（用户：停了还显示预计发送时间=残留脏文案）
        BtnStatus.Visibility = Visibility.Collapsed;
        Render();
        AddLogEntry(why, Array.Empty<string>());
    }

    string DisplayName() => _targetThreadName.Length > 0 ? _targetThreadName : Loc.T("status.none");

    /// <summary>运行态锁定续跑指令区：提示词/附件/发送时间/对话/循环选项灰显禁改，
    /// 要改=停止→改→再运行。</summary>
    void SetCommandLocked(bool locked)
    {
        foreach (var el in new FrameworkElement[] { BoxPrompt, AttachList, ConvoRow, SendTimeContent, LoopSend })
        {
            el.IsEnabled = !locked;
            el.BeginAnimation(OpacityProperty,
                new DoubleAnimation(locked ? 0.5 : 1, Motion.Normal) { EasingFunction = Motion.DecelerateMax });
        }
        UpdateCommandState();
    }

    /// <summary>Crossfade the run key's face to the stronger stop-state accent.</summary>
    void SetRunFace(bool danger)
    {
        BtnRun.ApplyTemplate();
        if (BtnRun.Template.FindName("dangerFace", BtnRun) is Border face)
            face.BeginAnimation(OpacityProperty,
                new DoubleAnimation(danger ? 1 : 0, Motion.Normal) { EasingFunction = Motion.DecelerateMax });
    }

    void UpdateScanInterval()
    {
        _scanTimer.Stop();
        _scanTimer.Interval = _settings.GetScanInterval(_armed);
        if (_scanTask is not { IsCompleted: false }) _scanTimer.Start();
    }

    void UpdateManualTimer()
    {
        _manualTimer.Stop();
        if (!_armed || _mode != RunMode.Manual || _sending || _firedThisCycle || _manualSendAt is not { } at) return;
        DateTime due = at > _retryAfter ? at : _retryAfter;
        // Detection frequency must not postpone a manual send deadline.
        double ms = Math.Clamp((due - DateTime.Now).TotalMilliseconds, 1, int.MaxValue);
        _manualTimer.Interval = TimeSpan.FromMilliseconds(ms);
        _manualTimer.Start();
    }

    async void ManualTimer_Tick(object? sender, EventArgs e)
    {
        _manualTimer.Stop();
        if (!_armed || _mode != RunMode.Manual || _sending || _firedThisCycle) return;
        if (_manualSendAt is not { } at || DateTime.Now < at || DateTime.Now < _retryAfter)
        {
            UpdateManualTimer();
            return;
        }
        AddLogEntry(Loc.T("log.send", _targetThreadName), new[] { _targetThreadName });
        await FireAsync();
    }

    void LoopSend_Changed(object sender, RoutedEventArgs e)
    {
        var option = (CheckBox)sender;
        option.ApplyTemplate();
        if (option.Template.FindName("LoopFace", option) is not Border face) return;
        double opacity = option.IsChecked == true ? 1 : 0;
        if (!option.IsLoaded || !SystemParameters.ClientAreaAnimation)
        {
            face.BeginAnimation(OpacityProperty, null);
            face.Opacity = opacity;
            return;
        }
        face.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(opacity == 1 ? 160 : 120))
            { EasingFunction = Motion.SettleOut });
    }

    // ---------- send and complete the selected run policy ----------
    async System.Threading.Tasks.Task FireAsync()
    {
        if (!_armed || _sending || _firedThisCycle) return;
        int revision = _runRevision;
        _sending = true;
        _firedThisCycle = true;
        Render();

        string prompt = BoxPrompt.Text.Trim();
        string msg = CodexService.ComposeMessage(prompt, _attachments);
        if (msg.Length == 0) msg = "/Continue";

        // Fall back only when queue submission is known to have failed.
        CodexService.SendResult result;
        try
        {
            result = await _sendQueued(msg, _targetThreadId);
            if (!_armed || revision != _runRevision) return;
            if (result.Outcome == CodexService.SendOutcome.Failed)
                result = await _sendFallback(msg);
        }

        catch (Exception e)
        {
            // Codex can close or recreate its UIA tree while a send is in flight.
            result = new CodexService.SendResult(CodexService.SendOutcome.Unknown, e.Message);
        }

        if (!_armed || revision != _runRevision) return;
        switch (result.Outcome)
        {
            case CodexService.SendOutcome.Sent:
                _sending = false;
                if (_mode == RunMode.Auto && LoopSend.IsChecked == true)
                {
                    _sendAttempts = 0;
                    _blockedAttempts = 0;
                    _retryAfter = DateTime.MinValue;
                    _sawExhausted = _snap.IsExhausted(DateTime.Now);
                    _target = _sawExhausted ? _snap.ResetAt : null;
                    _exhaustedAt = null;
                    AddLogEntry(Loc.T("log.loopDone"), Array.Empty<string>());
                    Render();
                    return;
                }
                AddLogEntry(Loc.T("log.done"), Array.Empty<string>());
                // one-shot: stop running, clear prompt + attachments
                BoxPrompt.Text = "";
                ClearAttachments();
                Disarm("");
                return;

            case CodexService.SendOutcome.Unknown:
                Disarm(Loc.T("log.unconfirmed", result.Log));
                return;

            case CodexService.SendOutcome.BlockedByLimit:
                _blockedAttempts++;
                if (_blockedAttempts >= 30)
                {
                    _sending = false;
                    Disarm(Loc.T("log.failed", result.Log));
                    return;
                }
                _sending = false;
                _firedThisCycle = false;
                _retryAfter = DateTime.Now.AddMinutes(2);
                UpdateManualTimer();
                return;

            default:
                _sendAttempts++;
                if (_sendAttempts >= 5)
                {
                    _sending = false;
                    Disarm(Loc.T("log.failed", result.Log));
                    return;
                }
                _sending = false;
                _firedThisCycle = false;
                if (_mode == RunMode.Manual) _retryAfter = DateTime.Now + _settings.GetScanInterval(true);
                UpdateManualTimer();
                return;
        }
    }

    // ---------- settings mode ----------
    void LoadSettingsSelection()
    {
        _loading = true;
        switch (_settings.ToThemeMode())
        {
            case CodexContinue.ThemeMode.Light: ThemeLight.IsChecked = true; break;
            case CodexContinue.ThemeMode.Dark: ThemeDark.IsChecked = true; break;
            default: ThemeSystem.IsChecked = true; break;
        }
        switch (_settings.ToLang())
        {
            case Loc.Lang.Zh: LangZh.IsChecked = true; break;
            case Loc.Lang.En: LangEn.IsChecked = true; break;
            default: LangSystem.IsChecked = true; break;
        }
        foreach (RadioButton option in IdleFrequencyOptions.Children)
            option.IsChecked = (string)option.Tag == _settings.IdleScanSeconds.ToString(CultureInfo.InvariantCulture);
        foreach (RadioButton option in RunningFrequencyOptions.Children)
            option.IsChecked = (string)option.Tag == _settings.RunningScanSeconds.ToString(CultureInfo.InvariantCulture);
        foreach (RadioButton option in AccentOptions.Children)
            option.IsChecked = (string)option.Tag == _settings.Accent;
        TransparencyOn.IsChecked = _settings.Transparency;
        TransparencyOff.IsChecked = !_settings.Transparency;
        FrequencyApplied.BeginAnimation(OpacityProperty, null);
        FrequencyApplied.Opacity = 0;
        _loading = false;
    }

    void Theme_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded) return;
        _settings.Theme = ThemeLight.IsChecked == true ? "light"
            : ThemeDark.IsChecked == true ? "dark" : "system";
        _settings.Save();
        ThemeManager.SetMode(_settings.ToThemeMode(), Application.Current);
    }

    void Accent_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded || sender is not RadioButton option) return;
        _settings.Accent = (string)option.Tag;
        _settings.Save();
        ThemeManager.SetAccent(_settings.Accent, Application.Current);
    }

    void Lang_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded) return;
        _settings.Language = LangZh.IsChecked == true ? "zh"
            : LangEn.IsChecked == true ? "en" : "system";
        _settings.Save();
        Loc.Set(_settings.ToLang());
        ApplyLoc();
        Render();
    }

    void UpdateDetectionHint() => RunningFrequencyHint.Text = Loc.T(
        _settings.RunningScanSeconds == 0 ? "settings.realtimeHint" : "settings.runningHint");

    void DetectionFrequency_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded || sender is not RadioButton option) return;
        int seconds = int.Parse((string)option.Tag, CultureInfo.InvariantCulture);
        bool idle = option.GroupName == "IdleFrequency";
        if (idle) _settings.IdleScanSeconds = seconds;
        else _settings.RunningScanSeconds = seconds;
        _settings.Save();
        if (idle != _armed) UpdateScanInterval();
        UpdateDetectionHint();
        var feedback = new DoubleAnimationUsingKeyFrames();
        feedback.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        feedback.KeyFrames.Add(new SplineDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150)),
            new KeySpline(0.215, 0.61, 0.355, 1)));
        feedback.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1400))));
        feedback.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1700))));
        FrequencyApplied.BeginAnimation(OpacityProperty, feedback);
    }
}
