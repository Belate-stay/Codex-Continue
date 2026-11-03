using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexContinue;

static class SendLifecycleChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
    static readonly CodexService.SendResult Sent = new(CodexService.SendOutcome.Sent, "accepted");

    public static void Once() => WithWindow(main =>
    {
        var loop = Find<CheckBox>(main, "LoopSend");
        Require(loop.IsChecked == false, "Automatic looping must start unchecked");
        var settings = Field<AppSettings>(main, "_settings");
        int queueCalls = 0, fallbackCalls = 0;
        Set(main, "_sendQueued", (Func<string, string?, Task<CodexService.SendResult>>)((_, _) =>
        { queueCalls++; return Task.FromResult(Sent); }));
        Set(main, "_sendFallback", (Func<string, Task<CodexService.SendResult>>)(_ =>
        { fallbackCalls++; return Task.FromResult(Sent); }));
        foreach (string mode in new[] { "Auto", "Manual" })
        {
            SetMode(main, mode);
            loop.IsChecked = mode == "Manual"; // A previous automatic choice cannot enable manual loops.
            Prepare(main);
            Invoke(main, "Arm");
            Wait((Task)Invoke(main, "FireAsync")!);
            Require(!Field<bool>(main, "_armed") && !Field<bool>(main, "_sending"), mode + " did not stop after success");
            Require(Equals(Find<Button>(main, "BtnRun").Content, Loc.T("cmd.run")), "Stop face was not restored");
            Require(Find<TextBox>(main, "BoxPrompt").IsEnabled && loop.IsEnabled, "Success did not unlock the command");
            Require(Field<DispatcherTimer>(main, "_scanTimer").Interval == settings.GetScanInterval(false), "Success kept the running cadence");
            Require(!Field<DispatcherTimer>(main, "_manualTimer").IsEnabled, "Success left a send timer running");
            Require(Find<TextBox>(main, "BoxPrompt").Text == "" && Field<List<string>>(main, "_attachments").Count == 0,
                "One-shot completion did not retain the existing clear behavior");
        }
        Require(queueCalls == 2 && fallbackCalls == 0, "A successful submission was sent more than once");
    });

    public static void Cycles() => WithWindow(main =>
    {
        var loop = Find<CheckBox>(main, "LoopSend");
        loop.IsChecked = true;
        Prepare(main);
        int sent = 0;
        Set(main, "_sendQueued", (Func<string, string?, Task<CodexService.SendResult>>)((message, thread) =>
        {
            Require(thread == "lifecycle-thread" && message.Contains("/Continue") && message.Contains("test.txt"),
                "A loop lost its selected chat, command or attachments");
            sent++;
            return Task.FromResult(Sent);
        }));
        Invoke(main, "Arm");
        Require(!loop.IsEnabled && ToolTipService.GetShowOnDisabled(loop), "The running choice must lock while hover help remains available");
        var now = DateTime.Now;
        var exhausted = new QuotaSnapshot(QuotaState.Limited, now.AddMinutes(3), "limit", 100, BannerVisible: true);
        var ready = new QuotaSnapshot(QuotaState.Ready, now.AddHours(5), "ready", 20);
        Require(!Observe(main, exhausted), "An unexpired exhausted quota triggered a send");
        Require(!Observe(main, QuotaSnapshot.Unknown("offline")), "Losing connection was treated as quota recovery");
        Require(Observe(main, ready), "The first recovery did not trigger");
        Set(main, "_sendAttempts", 4);
        Set(main, "_blockedAttempts", 29);
        Wait((Task)Invoke(main, "FireAsync")!);
        Require(Field<bool>(main, "_armed") && !Field<bool>(main, "_sending"), "A loop stopped after the first success");
        Require(Field<int>(main, "_sendAttempts") == 0 && Field<int>(main, "_blockedAttempts") == 0,
            "Retry limits leaked into the next cycle");
        Require(Find<TextBox>(main, "BoxPrompt").Text == "/Continue" && Field<List<string>>(main, "_attachments").Count == 1,
            "A loop cleared the instruction or attachments");
        Require(Field<DispatcherTimer>(main, "_scanTimer").Interval == Field<AppSettings>(main, "_settings").GetScanInterval(true),
            "Loop success restored the idle cadence");
        for (int i = 0; i < 5; i++) Require(!Observe(main, ready), "Healthy polling repeated the command");
        Require(!Observe(main, exhausted with { ResetAt = now.AddHours(8) }), "The next exhaustion sent before recovery");
        Require(Observe(main, ready with { ResetAt = now.AddHours(13) }), "A second quota cycle never rearmed");
        Wait((Task)Invoke(main, "FireAsync")!);
        Require(sent == 2 && Field<bool>(main, "_armed"), "The second cycle did not continue running");

        // A stale banner may permit a delayed first send, but must not create another cycle.
        Invoke(main, "Disarm", "");
        Prepare(main);
        var stale = exhausted with { ResetAt = now.AddMinutes(-4) };
        Set(main, "_snap", stale);
        Invoke(main, "Arm");
        Require(Observe(main, stale), "The existing stale-banner recovery fallback was lost");
        Wait((Task)Invoke(main, "FireAsync")!);
        Require(!Observe(main, stale) && !Observe(main, stale), "An unchanged stale banner repeatedly sent");
        Require(!Observe(main, QuotaSnapshot.Unknown("offline")), "Offline state reset the cycle latch");
        Require(!Observe(main, ready), "A completed stale-banner cycle sent again on recovery");
        Require(!Observe(main, exhausted), "A fresh cycle sent before recovery");
        Require(Observe(main, ready), "A fresh exhaustion did not restore normal looping");
    });

    public static void DeliveryAndStop() => WithWindow(main =>
    {
        var loop = Find<CheckBox>(main, "LoopSend");
        loop.IsChecked = true;
        int fallbackCalls = 0;
        Set(main, "_sendFallback", (Func<string, Task<CodexService.SendResult>>)(_ =>
        { fallbackCalls++; return Task.FromResult(Sent); }));
        Set(main, "_sendQueued", (Func<string, string?, Task<CodexService.SendResult>>)((_, _) =>
            Task.FromResult(new CodexService.SendResult(CodexService.SendOutcome.Unknown, "timeout"))));
        Prepare(main);
        Invoke(main, "Arm");
        Wait((Task)Invoke(main, "FireAsync")!);
        Require(!Field<bool>(main, "_armed") && fallbackCalls == 0, "An uncertain delivery was resent automatically");
        Require(Find<TextBox>(main, "BoxPrompt").Text == "/Continue", "An uncertain delivery erased the instruction");

        Set(main, "_sendQueued", (Func<string, string?, Task<CodexService.SendResult>>)((_, _) =>
            Task.FromException<CodexService.SendResult>(new IOException("connection lost during submission"))));
        Prepare(main);
        Invoke(main, "Arm");
        Wait((Task)Invoke(main, "FireAsync")!);
        Require(!Field<bool>(main, "_armed") && fallbackCalls == 0, "A transport interruption kept sending automatically");

        loop.IsChecked = false;
        Set(main, "_sendQueued", (Func<string, string?, Task<CodexService.SendResult>>)((_, _) =>
            Task.FromResult(new CodexService.SendResult(CodexService.SendOutcome.Failed, "not queued"))));
        Prepare(main);
        Invoke(main, "Arm");
        Wait((Task)Invoke(main, "FireAsync")!);
        Require(!Field<bool>(main, "_armed") && fallbackCalls == 1, "A known queue failure lost its fallback or did not stop");

        var pending = new TaskCompletionSource<CodexService.SendResult>();
        Set(main, "_sendQueued", (Func<string, string?, Task<CodexService.SendResult>>)((_, _) => pending.Task));
        Prepare(main);
        Invoke(main, "Arm");
        var oldSend = (Task)Invoke(main, "FireAsync")!;
        Invoke(main, "Disarm", "");
        Find<TextBox>(main, "BoxPrompt").Text = "New instruction";
        Invoke(main, "Arm");
        pending.SetResult(Sent);
        Wait(oldSend);
        Require(Field<bool>(main, "_armed") && Find<TextBox>(main, "BoxPrompt").Text == "New instruction",
            "A stopped send completed against a newer run");
        Require(!Field<bool>(main, "_sending"), "An old completion left the new run busy");
    });

    public static void QueueProcess()
    {
        foreach (string mode in new[] { "accepted", "failed", "timeout" })
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!)
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--queue-process-probe");
            start.ArgumentList.Add(mode);
            using var process = Process.Start(start)!;
            try
            {
                var watch = Stopwatch.StartNew();
                var method = typeof(CodexService).GetMethod("WaitForQueueExitAsync", Private)
                    ?? throw new Exception("Queue process timeout cannot bound stream reads");
                var task = (Task<CodexService.SendResult>)method.Invoke(null, new object[] { process, TimeSpan.FromSeconds(2) })!;
                Require(task.Wait(TimeSpan.FromSeconds(5)), "Queue output reads bypassed the process timeout");
                var expected = mode == "accepted" ? CodexService.SendOutcome.Sent : mode == "failed" ? CodexService.SendOutcome.Failed : CodexService.SendOutcome.Unknown;
                Require(task.Result.Outcome == expected, "Queue completion outcome was misclassified: " + mode);
                Require(watch.Elapsed < TimeSpan.FromSeconds(5), "The bounded queue process did not return promptly");
            }
            finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(3000); } }
        }
    }

    public static int Probe(string mode)
    {
        Console.Error.Write(new string('x', 100_000));
        Console.Out.Write("queued");
        Console.Out.Flush();
        if (mode == "timeout") Thread.Sleep(20_000);
        return mode == "failed" ? 1 : 0;
    }

    public static void Surface() => WithWindow(main =>
    {
        var originalTheme = ThemeManager.Mode;
        var originalLanguage = Loc.Current;
        var root = (Grid)main.Content;
        Find<Grid>(main, "SplashPage").Visibility = Visibility.Collapsed;
        main.Content = null;
        var host = new Window { Width = 1120, Height = 730, WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.CanMinimize, ShowActivated = false, UseLayoutRounding = true, Content = root };
        try
        {
            host.Show();
            Prepare(main);
            var row = Find<FrameworkElement>(main, "LoopPanel");
            var loop = Find<CheckBox>(main, "LoopSend");
            foreach (var theme in new[] { CodexContinue.ThemeMode.Light, CodexContinue.ThemeMode.Dark })
            foreach (var language in new[] { Loc.Lang.Zh, Loc.Lang.En })
            {
                ThemeManager.SetMode(theme, Application.Current);
                Loc.Set(language);
                Invoke(main, "ApplyLoc");
                loop.IsChecked = false;
                Pump(160);
                loop.IsChecked = true;
                Pump(45);
                var face = (Border)loop.Template.FindName("LoopFace", loop);
                Require(!SystemParameters.ClientAreaAnimation || (face.Opacity > 0 && face.Opacity < 1),
                    "The checkbox selection snaps instead of easing in");
                Pump(200);
                var picker = Find<ComboBox>(main, "ConvoPick");
                Require(Math.Abs(loop.TranslatePoint(new Point(), root).X - picker.TranslatePoint(new Point(), root).X) <= 1,
                    "The loop checkbox is not aligned with the chat input");
                Require(row.TranslatePoint(new Point(), root).Y >= picker.TranslatePoint(new Point(0, picker.ActualHeight), root).Y + 5,
                    "The loop option overlaps the picker");
                Require(row.TranslatePoint(new Point(0, row.ActualHeight), root).Y + 8 < Find<Button>(main, "BtnRun").TranslatePoint(new Point(), root).Y,
                    "The loop row crowds the action buttons");
                loop.ApplyTemplate();
                var selected = (Border)loop.Template.FindName("LoopFace", loop);
                Require(selected.Background is SolidColorBrush brush && brush.Color == ((SolidColorBrush)Application.Current.FindResource("AccentBrush")).Color,
                    "The checkbox ignores the current accent");
                Capture(root, $"loop-{theme}-{language}.png");
                Require(main.FindName("BtnLoopInfo") == null, "The circled exclamation button was not removed");
                var tip = loop.ToolTip as ToolTip ?? throw new Exception("Loop help is not a hover tooltip");
                Require(tip.Placement == System.Windows.Controls.Primitives.PlacementMode.MousePoint &&
                    tip.Content is TextBlock text && text.Text == Loc.T("cmd.loopHelp") && text.TextWrapping == TextWrapping.Wrap,
                    "The pointer tooltip is missing the localized formal explanation");
                tip.PlacementTarget = loop;
                tip.IsOpen = true;
                Pump(100);
                tip.ApplyTemplate();
                tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                tip.Arrange(new Rect(tip.DesiredSize));
                var tipFace = (Border)tip.Template.FindName("ToolTipFace", tip);
                Require(tipFace.CornerRadius.TopLeft == 8 && tip.ActualWidth <= 364,
                    "The hover explanation lost its rounded face or text width limit");
                Require(tipFace.Background is SolidColorBrush paper && paper.Color == ((SolidColorBrush)Application.Current.FindResource("PillBrush")).Color,
                    "The hover explanation does not follow the current appearance");
                Capture(tip, $"loop-help-{theme}-{language}.png");
                tip.IsOpen = false;
                Select(main, "Manual");
                Pump(500);
                Require(row.Visibility == Visibility.Collapsed && Find<FrameworkElement>(main, "SendTimePanel").Visibility == Visibility.Visible,
                    "Manual Timer still offers looping");
                Capture(root, $"manual-once-{theme}-{language}.png");
                Select(main, "Auto");
                Pump(500);
                Require(row.Visibility == Visibility.Visible && loop.IsChecked == true, "Automatic choice was lost when returning from Manual Timer");
            }
        }
        finally
        {
            host.Content = null;
            host.Close();
            main.Content = root;
            Loc.Set(originalLanguage);
            ThemeManager.SetMode(originalTheme, Application.Current);
        }
    });

    static bool Observe(MainWindow main, QuotaSnapshot snapshot)
    {
        Set(main, "_snap", snapshot);
        return (bool)Invoke(main, "ShouldSendOnRecovery", snapshot)!;
    }
    static void Prepare(MainWindow main)
    {
        var thread = new CodexService.RunningThread("lifecycle-thread", "Lifecycle check", false);
        Set(main, "_threads", new List<CodexService.RunningThread> { thread });
        Invoke(main, "RefreshTargetThread");
        Find<ComboBox>(main, "ConvoPick").SelectedIndex = 0;
        Find<TextBox>(main, "BoxPrompt").Text = "/Continue";
        Field<List<string>>(main, "_attachments").Clear();
        Field<List<string>>(main, "_attachments").Add("test.txt");
        Set(main, "_snap", new QuotaSnapshot(QuotaState.Ready, DateTime.Now.AddHours(5), "test", 20));
    }
    static void WithWindow(Action<MainWindow> check)
    {
        var main = new MainWindow();
        Invoke(main, "ApplyLoc");
        try { check(main); }
        finally
        {
            Field<DispatcherTimer>(main, "_scanTimer").Stop();
            Field<DispatcherTimer>(main, "_manualTimer").Stop();
            Set(main, "_forceClose", true);
            main.Close();
        }
    }
    static void SetMode(MainWindow main, string value) => Set(main, "_mode", Enum.Parse(typeof(MainWindow).GetNestedType("RunMode", BindingFlags.NonPublic)!, value));
    static void Select(MainWindow main, string value) => Invoke(main, "SwitchView", Enum.Parse(typeof(MainWindow).GetNestedType("ViewMode", BindingFlags.NonPublic)!, value));
    static T Find<T>(MainWindow main, string name) where T : class => main.FindName(name) as T ?? throw new Exception("Missing loop control " + name);
    static object? Invoke(MainWindow main, string name, params object[] args) => (typeof(MainWindow).GetMethod(name, Private)
        ?? throw new Exception("Missing send lifecycle method " + name)).Invoke(main, args);
    static T Field<T>(MainWindow main, string name) => (T)typeof(MainWindow).GetField(name, Private)!.GetValue(main)!;
    static void Set(MainWindow main, string name, object value) => (typeof(MainWindow).GetField(name, Private)
        ?? throw new Exception("Missing isolated transport " + name)).SetValue(main, value);
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Wait(Task task)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(3)) Pump(10);
        Require(task.IsCompleted, "Isolated send did not finish");
        task.GetAwaiter().GetResult();
    }
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
    static void Capture(FrameworkElement root, string name)
    {
        var bitmap = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            var bounds = new Rect(0, 0, root.ActualWidth, root.ActualHeight);
            drawing.DrawRectangle((Brush)Application.Current.FindResource("CanvasBrush"), null, bounds);
            drawing.DrawRectangle(new VisualBrush(root) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds }, null, bounds);
        }
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string directory = Path.Combine(AppContext.BaseDirectory, "Artifacts");
        Directory.CreateDirectory(directory);
        using var file = File.Create(Path.Combine(directory, name));
        encoder.Save(file);
    }
}
