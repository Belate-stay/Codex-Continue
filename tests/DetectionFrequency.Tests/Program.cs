using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CodexContinue;

static class Program
{
    static int _failures;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--queue-process-probe") return SendLifecycleChecks.Probe(args[1]);
        if (args.FirstOrDefault() == "--single-instance-probe")
        {
            try { return SingleInstanceChecks.Probe(args); }
            catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        }
        // Component fixtures load the real resources without running product startup.
        var startupOperations = new List<DispatcherOperation>();
        DispatcherHookEventHandler capture = (_, e) => startupOperations.Add(e.Operation);
        Dispatcher.CurrentDispatcher.Hooks.OperationPosted += capture;
        var app = new App();
        Dispatcher.CurrentDispatcher.Hooks.OperationPosted -= capture;
        foreach (var operation in startupOperations) operation.Abort();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        if (args.FirstOrDefault() == "--release-only")
        {
            Check("Version 1.0 labels and tags compare correctly", UpdateChecks.Versions);
            Check("production startup stays quiet without a newer real release", UpdateChecks.ProductionStartup);
            Check("new real releases show the normal card and retain update/download behavior", UpdateChecks.Surface);
            Console.WriteLine(_failures == 0 ? "PASS: 0 failed checks" : $"FAIL: {_failures} failed checks");
            return _failures == 0 ? 0 : 1;
        }
        Check("glass defaults off and system accent preferences persist", AppearanceChecks.Preferences);
        Check("loop check is visually centered and help contains only the enabled mechanism", AppearanceChecks.Loop);
        Check("native frost preserves readable controls and adapts to settings layouts", AppearanceChecks.Material);
        Check("system color notifications refresh the live accent with readable palette text", AppearanceChecks.SystemAccent);
        Check("glass startup measures the quota before revealing its cached reading", AppearanceChecks.StartupQuota);
        if (args.FirstOrDefault() == "--appearance-only")
        {
            Console.WriteLine(_failures == 0 ? "PASS: 0 failed checks" : $"FAIL: {_failures} failed checks");
            return _failures == 0 ? 0 : 1;
        }
        if (args.FirstOrDefault() == "--release-nav-only")
        {
            Check("update details retain the dated footer and preserve download/navigation state", UpdateChecks.Surface);
            var surface = new MainWindow();
            try
            {
                Check("navigation hover stays quiet and uses the hand cursor", () => InlineModeChecks.MenuHover(surface));
                Check("selected modes keep their card and icon feedback", () => InlineModeChecks.Navigation(surface));
                Check("the selected indicator retains its eased travel", () => InlineModeChecks.Slider(surface));
            }
            finally { SetField(surface, "_forceClose", true); surface.Close(); }
            Console.WriteLine(_failures == 0 ? "PASS: 0 failed checks" : $"FAIL: {_failures} failed checks");
            return _failures == 0 ? 0 : 1;
        }
        if (args.FirstOrDefault() == "--tray-menu-only")
        {
            Check("tray menu order, rounded styles, state feedback and shortcuts preserve the active draft", TrayChecks.Menu);
            return _failures == 0 ? 0 : 1;
        }
        Check("borderless controls retain soft depth, flat selections and visible keyboard focus", BorderlessChecks.Surface);
        if (args.FirstOrDefault() == "--surface-only")
        {
            Console.WriteLine(_failures == 0 ? "PASS: 0 failed checks" : $"FAIL: {_failures} failed checks");
            return _failures == 0 ? 0 : 1;
        }
        Check("startup title travels continuously into the header and survives navigation, resize and retry", StartupMotionChecks.Sequence);
        if (args.FirstOrDefault() == "--startup-only")
        {
            Console.WriteLine(_failures == 0 ? "PASS: 0 failed checks" : $"FAIL: {_failures} failed checks");
            return _failures == 0 ? 0 : 1;
        }
        Check("tray menu order, rounded styles, state feedback and shortcuts preserve the active draft", TrayChecks.Menu);
        if (args.FirstOrDefault() == "--tray-only")
        {
            Check("close-to-tray preserves armed work and explicit Exit cleans up", TrayChecks.Lifecycle);
            Check("idle close retains detection and session shutdown", TrayChecks.Idle);
            Console.WriteLine(_failures == 0 ? "PASS: 0 failed checks" : $"FAIL: {_failures} failed checks");
            return _failures == 0 ? 0 : 1;
        }
        Check("the window resizes, maximizes to the work area and restores its previous state", AdaptiveWindowChecks.Shell);
        if (args.FirstOrDefault() == "--adaptive-shell") return _failures == 0 ? 0 : 1;
        Check("window proportions retain readable, accessible layouts and bounded content", AdaptiveWindowChecks.Layouts);
        if (args.FirstOrDefault() == "--adaptive-only")
        {
            Console.WriteLine(_failures == 0 ? "PASS: 0 failed checks" : $"FAIL: {_failures} failed checks");
            return _failures == 0 ? 0 : 1;
        }
        Check("update versions compare prereleases and numeric identifiers correctly", UpdateChecks.Versions);
        Check("production startup stays quiet without a newer real release", UpdateChecks.ProductionStartup);
        Check("release checks include Beta builds and select only compatible Windows packages", UpdateChecks.Releases);
        Check("downloads verify integrity and preserve completed files on failure", UpdateChecks.Downloads);
        Check("real startup notifications, manual updates and details/back preserve the active task", UpdateChecks.Surface);
        Check("single automatic and manual sends stop, unlock and restore idle cadence", SendLifecycleChecks.Once);
        Check("looping waits for fresh quota cycles and preserves the exact command", SendLifecycleChecks.Cycles);
        Check("uncertain delivery never resends and stopped callbacks cannot change a newer run", SendLifecycleChecks.DeliveryAndStop);
        Check("queue process output and timeout are bounded without desktop input", SendLifecycleChecks.QueueProcess);
        Check("loop control and formal help align in both appearances and languages; Manual Timer stays one-shot", SendLifecycleChecks.Surface);
        Check("accent presets default, validate and persist without changing legacy preferences", AccentColorChecks.Settings);
        Check("duplicate launches notify one owner and release the guard on exit", SingleInstanceChecks.DuplicateLaunches);
        Check("concurrent launches elect one owner and recover after a crash", SingleInstanceChecks.ConcurrentAndCrashRecovery);
        Check("new settings default to idle 5 min / running 1 min", () =>
        {
            var s = new AppSettings();
            Equal(300, Seconds(s, "IdleScanSeconds"));
            Equal(60, Seconds(s, "RunningScanSeconds"));
        });
        Check("legacy settings retain theme/language and receive new defaults", () =>
        {
            var s = JsonSerializer.Deserialize<AppSettings>("{\"Theme\":\"dark\",\"Language\":\"en\"}")!;
            Equal("dark", s.Theme);
            Equal("en", s.Language);
            Equal(300, Seconds(s, "IdleScanSeconds"));
            Equal(60, Seconds(s, "RunningScanSeconds"));
        });
        Check("all idle choices persist and resolve to their selected interval", () =>
        {
            foreach (int seconds in new[] { 60, 180, 300, 600, 1800 })
            {
                var s = RoundTrip("IdleScanSeconds", seconds);
                Equal(seconds, Seconds(s, "IdleScanSeconds"));
                Equal(TimeSpan.FromSeconds(seconds), Interval(s, false));
                Equal(TimeSpan.FromSeconds(60), Interval(s, true));
            }
        });
        Check("all running choices persist; realtime is paced at one second", () =>
        {
            foreach (int seconds in new[] { 0, 30, 60, 180, 300 })
            {
                var s = RoundTrip("RunningScanSeconds", seconds);
                Equal(seconds, Seconds(s, "RunningScanSeconds"));
                Equal(TimeSpan.FromSeconds(Math.Max(1, seconds)), Interval(s, true));
                Equal(TimeSpan.FromMinutes(5), Interval(s, false));
            }
        });
        Check("invalid stored intervals fall back to safe defaults", () =>
        {
            foreach (int seconds in new[] { -1, 1, 59, 3600, int.MaxValue })
            {
                var s = JsonSerializer.Deserialize<AppSettings>(
                    $"{{\"IdleScanSeconds\":{seconds},\"RunningScanSeconds\":{seconds}}}")!;
                Equal(300, Seconds(s, "IdleScanSeconds"));
                Equal(60, Seconds(s, "RunningScanSeconds"));
            }
            Equal(300, Seconds(RoundTrip("IdleScanSeconds", 0), "IdleScanSeconds"));
        });
        Check("RPC reads time out and reset the reader instead of stalling detection", () =>
        {
            using var rpc = new RateLimitsClient();
            using var writer = new AnonymousPipeServerStream(PipeDirection.Out);
            using var reader = new AnonymousPipeClientStream(PipeDirection.In, writer.ClientSafePipeHandle);
            using var text = new StreamReader(reader);
            SetField(rpc, "_reader", text);
            var sw = Stopwatch.StartNew();
            Equal<string?>(null, (string?)Invoke(rpc, "ReadLine", TimeSpan.FromMilliseconds(80)));
            Equal(true, sw.Elapsed < TimeSpan.FromSeconds(2));
            Equal<StreamReader?>(null, Field<StreamReader?>(rpc, "_reader"));
        });

        var window = new MainWindow();
        var timer = Field<DispatcherTimer>(window, "_scanTimer");
        try
        {
            Check("Run requires an explicit conversation and a nonempty command", () => CommandFormChecks.Run(window));
            Check("disabled Run and rounded tooltips follow themes and language", () => CommandFormChecks.Tooltips(window));
            Check("settings expose exactly the requested two sets of choices", () =>
            {
                ChoiceTags(window, "IdleFrequencyOptions", new[] { 60, 180, 300, 600, 1800 });
                ChoiceTags(window, "RunningFrequencyOptions", new[] { 0, 30, 60, 180, 300 });
            });
            Check("idle schedule remains active while disconnected", () =>
            {
                var s = Field<AppSettings>(window, "_settings");
                var conn = typeof(MainWindow).GetNestedType("ConnState", BindingFlags.NonPublic)!;
                SetField(window, "_conn", Enum.Parse(conn, "Offline"));
                SetField(window, "_armed", false);
                foreach (int seconds in new[] { 60, 180, 300, 600, 1800 })
                {
                    SetSeconds(s, "IdleScanSeconds", seconds);
                    Invoke(window, "UpdateScanInterval");
                    Equal(TimeSpan.FromSeconds(seconds), timer.Interval);
                    Equal(true, timer.IsEnabled);
                }
            });
            var testConversations = (ComboBox)window.FindName("ConvoPick");
            testConversations.ItemsSource = new[] { new CodexService.RunningThread("test-command", "Test conversation", false) };
            testConversations.SelectedIndex = 0;
            ((TextBox)window.FindName("BoxPrompt")).Text = "/Continue";
            Check("Run and Stop switch cadence and lock/unlock the command editor", () =>
            {
                var s = Field<AppSettings>(window, "_settings");
                SetSeconds(s, "IdleScanSeconds", 180);
                SetSeconds(s, "RunningScanSeconds", 0);
                Invoke(window, "Arm");
                Equal(TimeSpan.FromSeconds(1), timer.Interval);
                Equal(false, ((TextBox)window.FindName("BoxPrompt")).IsEnabled);
                Invoke(window, "Disarm", "test stopped");
                Equal(TimeSpan.FromMinutes(3), timer.Interval);
                Equal(true, ((TextBox)window.FindName("BoxPrompt")).IsEnabled);
            });
            Check("running and stop transitions use their own selected frequency", () =>
            {
                var s = Field<AppSettings>(window, "_settings");
                SetSeconds(s, "IdleScanSeconds", 600);
                foreach (int seconds in new[] { 0, 30, 60, 180, 300 })
                {
                    SetSeconds(s, "RunningScanSeconds", seconds);
                    SetField(window, "_armed", true);
                    Invoke(window, "UpdateScanInterval");
                    Equal(TimeSpan.FromSeconds(Math.Max(1, seconds)), timer.Interval);
                    SetField(window, "_armed", false);
                    Invoke(window, "UpdateScanInterval");
                    Equal(TimeSpan.FromMinutes(10), timer.Interval);
                }
            });
            Check("manual and automatic requests share an in-flight detection", () =>
            {
                var pending = new TaskCompletionSource();
                SetField(window, "_scanTask", pending.Task);
                Equal(pending.Task, (Task)Invoke(window, "ScanAsync")!);
                Equal(pending.Task, (Task)Invoke(window, "ScanAsync")!);
                pending.SetResult();
            });
            Check("manual send deadline is independent of a five-minute detection interval", () =>
            {
                SetField(window, "_scanTask", Task.CompletedTask);
                var mode = typeof(MainWindow).GetNestedType("RunMode", BindingFlags.NonPublic)!;
                SetField(window, "_mode", Enum.Parse(mode, "Manual"));
                SetSeconds(Field<AppSettings>(window, "_settings"), "RunningScanSeconds", 300);
                Invoke(window, "Arm");
                SetField(window, "_manualSendAt", DateTime.Now.AddSeconds(20));
                Invoke(window, "UpdateManualTimer");
                var deadline = Field<DispatcherTimer>(window, "_manualTimer");
                Equal(TimeSpan.FromMinutes(5), timer.Interval);
                Equal(true, deadline.IsEnabled);
                Equal(true, deadline.Interval.TotalSeconds > 18 && deadline.Interval.TotalSeconds <= 20);
                Invoke(window, "Disarm", "test stopped");
                Equal(false, deadline.IsEnabled);
            });
            Check("settings and log use inline modes with one blue selected card", () => InlineModeChecks.Navigation(window));
            Check("mode dissolves survive rapid navigation and retain the armed draft", () => InlineModeChecks.Transitions(window));
            Check("automatic/manual switches fade only their unique controls and keep the shared content stable", () => InlineModeChecks.RunModes(window));
            Check("mode indicator slides on two tracks, hides in the gap and accelerates long travel", () => InlineModeChecks.Slider(window));
            Check("navigation hover stays quiet and uses the hand cursor", () => InlineModeChecks.MenuHover(window));
            Check("accent cards animate, follow appearance and preserve quota and semantic ink", () => AccentColorChecks.Palette(window));
            Check("conversation menu animates closure and keeps slim scrolling functional", () =>
                ConversationMenuChecks.Run(window));
            Check("quota losses trail behind the live fill and band colors crossfade", () =>
                QuotaMeterChecks.Run(window));
            Check("close-to-tray preserves armed work; restore and explicit Exit keep the correct lifecycle", TrayChecks.Lifecycle);
            Check("idle close keeps detection alive and Windows shutdown can still exit", TrayChecks.Idle);
        }
        finally
        {
            timer.Stop();
            SetField(window, "_armed", false);
            SetField(window, "_forceClose", true);
            window.Close();
            app.Shutdown();
        }
        Console.WriteLine($"{(_failures == 0 ? "PASS" : "FAIL")}: {_failures} failed checks");
        return _failures == 0 ? 0 : 1;
    }

    static void Check(string name, Action action)
    {
        try { action(); Console.WriteLine("PASS " + name); }
        catch (Exception e) { _failures++; Console.WriteLine("FAIL " + name + ": " + (e.InnerException ?? e)); }
    }

    static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Expected {expected}, got {actual}");
    }

    static PropertyInfo Property(string name) => typeof(AppSettings).GetProperty(name)
        ?? throw new Exception("Missing detection-frequency setting: " + name);
    static int Seconds(AppSettings settings, string name) => (int)Property(name).GetValue(settings)!;
    static void SetSeconds(AppSettings settings, string name, int seconds) => Property(name).SetValue(settings, seconds);
    static AppSettings RoundTrip(string name, int seconds)
    {
        var s = new AppSettings();
        SetSeconds(s, name, seconds);
        return JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s))!;
    }
    static TimeSpan Interval(AppSettings s, bool running) => (TimeSpan)(typeof(AppSettings).GetMethod("GetScanInterval")
        ?? throw new Exception("Missing state-dependent scan interval")).Invoke(s, new object[] { running })!;
    static T Field<T>(object target, string name) => (T)(target.GetType().GetField(name, Private)
        ?? throw new Exception("Missing field " + name)).GetValue(target)!;
    static void SetField(object target, string name, object value) => (target.GetType().GetField(name, Private)
        ?? throw new Exception("Missing field " + name)).SetValue(target, value);
    static object? Invoke(object target, string name, params object[] args) => (target.GetType().GetMethod(name, Private)
        ?? throw new Exception("Missing method " + name)).Invoke(target, args);
    static void ChoiceTags(MainWindow window, string name, int[] expected)
    {
        var panel = window.FindName(name) as Panel ?? throw new Exception("Missing frequency choice row " + name);
        var radios = panel.Children.OfType<RadioButton>().ToArray();
        Equal(string.Join(",", expected), string.Join(",", radios.Select(r => Convert.ToInt32(r.Tag))));
        Equal(1, radios.Select(r => r.GroupName).Distinct().Count());
    }
}

