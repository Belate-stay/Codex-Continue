using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexContinue;

static class UpdateChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static Type VersionType => typeof(App).Assembly.GetType("CodexContinue.ReleaseVersion") ?? throw new Exception("Missing semantic update version parser");
    static Type ClientType => typeof(App).Assembly.GetType("CodexContinue.GitHubUpdates") ?? throw new Exception("Missing GitHub update client");
    static readonly byte[] Package = Encoding.UTF8.GetBytes("deterministic update download fixture");

    public static void Versions()
    {
        Require(typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0] == "Version 1.0",
            "The release does not display Version 1.0");
        foreach (var (older, newer) in new[] { ("Beta 0.9.0", "Beta 0.9.1"), ("v0.9.1-beta.2", "v0.9.1-beta.10"),
            ("0.9.1-beta", "0.9.1"), ("0.9.2", "0.9.10"), ("0.9.1-alpha", "0.9.1-beta"),
            ("Beta 0.9.2", "Version 1.0"), ("1.0.0-beta", "Version 1.0"), ("Version 1.0", "v1.0.1") })
            Require((int)VersionType.GetMethod("CompareTo")!.Invoke(Version(older), new[] { Version(newer) })! < 0, "Incorrect version ordering: " + older + " / " + newer);
        foreach (string alias in new[] { "Version 1.0", "v1.0", "1.0", "v1.0.0" })
            Require((int)VersionType.GetMethod("CompareTo")!.Invoke(Version(alias), new[] { Version("1.0.0") })! == 0,
                "Version 1.0 aliases compare as a different release: " + alias);
        Require((int)VersionType.GetMethod("CompareTo")!.Invoke(Version("v0.9.1-beta+build.2"), new[] { Version("Beta 0.9.1") })! == 0,
            "Build metadata changes version precedence");
        foreach (string invalid in new[] { "", "latest", "1", "../0.9.1", "0.9.1-beta.01" })
            Require(VersionType.GetMethod("Parse")!.Invoke(null, new object[] { invalid }) == null, "Malformed tag accepted: " + invalid);
    }

    public static void Releases()
    {
        string body = "[" + Release("v0.9.0-beta") + "," + Release("v0.9.9-beta", draft: true) + "," + Release("v0.9.2-beta.1") + "," + Release("invalid") + "]";
        using var http = new HttpClient(new Handler((request, _) =>
        {
            Require(request.RequestUri!.Host == "api.github.com" && request.RequestUri.AbsolutePath.EndsWith("/releases"), "Update checks do not use the public release list");
            Require(request.Headers.UserAgent.Any(), "GitHub requests lack a User-Agent");
            return Task.FromResult(Json(body));
        }));
        using var client = Client(http);
        var release = Find(client, "Beta 0.9.1");
        Require(release != null && Property<string>(release, "TagName") == "v0.9.2-beta.1", "Draft/old/invalid releases hide the newer prerelease");
        var asset = Property<object>(release!, "Asset");
        Require(Property<string>(asset, "Name") == "Codex-Continue.exe", "The correctly named Windows package was not selected");
        Require(Find(client, "0.9.2") == null, "An older prerelease is offered to a newer installed version");
        using var emptyHttp = new HttpClient(new Handler((_, _) => Task.FromResult(Json("[]"))));
        using var empty = Client(emptyHttp);
        Require(Find(empty, "Beta 0.9.1") == null, "An empty published release list invented an update");
        using var failedHttp = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden))));
        using var failed = Client(failedHttp);
        RequireThrows(() => Find(failed, "Beta 0.9.1"), "HTTP errors were reported as an up-to-date result");
    }

    public static void Downloads()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Artifacts", "update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var http = new HttpClient(new Handler((request, _) => Task.FromResult(request.RequestUri!.Host == "api.github.com"
                ? Json("[" + Release("v0.9.2-beta") + "]") : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Package) })));
            using var client = Client(http);
            var release = Find(client, "Beta 0.9.1")!;
            string path = Download(client, release, directory);
            Require(File.ReadAllBytes(path).SequenceEqual(Package), "The downloaded package was corrupted");
            Require(!Directory.EnumerateFiles(directory, "*.part", SearchOption.AllDirectories).Any(), "A completed download left a partial file");
            using var brokenHttp = new HttpClient(new Handler((request, _) => Task.FromResult(request.RequestUri!.Host == "api.github.com"
                ? Json("[" + Release("v0.9.2-beta", hash: new string('0', 64)) + "]") : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Package) })));
            using var broken = Client(brokenHttp);
            RequireThrows(() => Download(broken, Find(broken, "Beta 0.9.1")!, directory), "A mismatched checksum was accepted");
            Require(File.ReadAllBytes(path).SequenceEqual(Package), "A failed download overwrote the existing complete package");
            Require(!Directory.EnumerateFiles(directory, "*.part", SearchOption.AllDirectories).Any(), "A failed download left a partial file");
            using var interruptedHttp = new HttpClient(new Handler((request, _) => Task.FromResult(request.RequestUri!.Host == "api.github.com"
                ? Json("[" + Release("v0.9.3-beta") + "]") : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new InterruptedStream()) })));
            using var interrupted = Client(interruptedHttp);
            RequireThrows(() => Download(interrupted, Find(interrupted, "Beta 0.9.1")!, directory), "A broken connection produced a completed package");
            Require(!Directory.EnumerateFiles(directory, "*.part", SearchOption.AllDirectories).Any(), "An interrupted stream left a partial file");
            using var cancellation = new CancellationTokenSource();
            using var cancelledHttp = new HttpClient(new Handler(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Json("[]"); }));
            using var cancelled = Client(cancelledHttp);
            cancellation.CancelAfter(60);
            RequireThrows(() => Download(cancelled, release, Path.Combine(directory, "cancelled"), cancellation.Token), "Cancellation did not stop a pending download");
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) File.Delete(file);
            foreach (string folder in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length)) Directory.Delete(folder);
            Directory.Delete(directory);
        }
    }

    public static void ProductionStartup()
    {
        var context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var main = new MainWindow();
        try
        {
            foreach (string body in new[] { "[]", "[" + Release("v1.0.0") + "," + Release("v0.9.2-beta") + "]" })
            {
                using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(body))));
                using var client = Client(http); Set(main, "_updates", client);
                Invoke(main, "StartUpdateCheck"); Wait(Field<Task>(main, "_updateCheckTask")); Pump(100);
                Require(Find<FrameworkElement>(main, "UpdateToast").Visibility == Visibility.Collapsed && Field<object?>(main, "_toastUpdate") == null,
                    "Production startup invents a preview/current/older-version update notification");
            }
            using var failedHttp = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden))));
            using var failed = Client(failedHttp); Set(main, "_updates", failed);
            Invoke(main, "StartUpdateCheck"); Wait(Field<Task>(main, "_updateCheckTask")); Pump(100);
            Require(Find<FrameworkElement>(main, "UpdateToast").Visibility == Visibility.Collapsed,
                "A failed background check displays a fabricated update");
        }
        finally
        {
            Set(main, "_forceClose", true); main.Close(); SynchronizationContext.SetSynchronizationContext(context);
        }
    }

    public static void Surface()
    {
        var context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var main = new MainWindow();
        var theme = ThemeManager.Mode;
        var language = Loc.Current;
        var root = (Grid)main.Content;
        main.Content = null;
        var host = new Window { Width = 1120, Height = 730, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.CanMinimize,
            ShowActivated = false, UseLayoutRounding = true, Content = root };
        int calls = 0;
        var pending = new TaskCompletionSource<HttpResponseMessage>();
        var pendingManual = new TaskCompletionSource<HttpResponseMessage>();
        using var http = new HttpClient(new Handler((_, _) => { calls++; return calls == 1 ? pending.Task : pendingManual.Task; }));
        using var client = Client(http);
        Set(main, "_updates", client);
        try
        {
            Invoke(main, "ApplyLoc");
            host.Show();
            Invoke(main, "StartUpdateCheck");
            Pump(300);
            Require(Find<FrameworkElement>(main, "UpdateToast").Visibility == Visibility.Collapsed, "Startup still forces the preview before a real check completes");
            var previousNavigation = Find<FrameworkElement>(main, "TabRow").Visibility;
            pending.SetResult(Json("[" + Release("v1.0.1") + "]"));
            Wait(Field<Task>(main, "_updateCheckTask")); Pump(300);
            Require(Find<FrameworkElement>(main, "UpdateToast").Visibility == Visibility.Visible && !Field<UpdateRelease>(main, "_toastUpdate").IsPreview,
                "A detected newer real release does not show its update card");
            Require(Find<TextBlock>(main, "UpdateToastTitle").Text == Loc.T("updates.available"), "The notification title differs from the requested copy");
            Capture(root, "updates-real-startup.png");
            Click(main, "BtnUpdateToastClose");
            Pump(240);
            Require(Find<FrameworkElement>(main, "UpdateToast").Visibility == Visibility.Collapsed, "The X does not dismiss the notification");
            Set(main, "_updateToastDismissed", false);
            Invoke(main, "ShowUpdateToast", Field<object>(main, "_toastUpdate"));
            Click(main, "BtnUpdateToastOpen");
            Pump(70);
            Require(Find<Button>(main, "BtnDownloadUpdate").IsEnabled && !Field<UpdateRelease>(main, "_shownUpdate").IsPreview,
                "A real update card opens an undownloadable preview");
            var background = (Task)Invoke(main, "CheckForUpdatesAsync", false)!;
            var manual = (Task)Invoke(main, "CheckForUpdatesAsync", true)!;
            Require(calls == 2 && ReferenceEquals(background, manual), "A manual check duplicates an in-flight background request");
            pendingManual.SetResult(Json("[" + Release("v1.0.1") + "]"));
            Wait(manual);
            Pump(300);
            Require(Find<FrameworkElement>(main, "UpdatePage").Visibility == Visibility.Visible &&
                Find<FrameworkElement>(main, "TabRow").Visibility == Visibility.Collapsed && Find<FrameworkElement>(main, "ContentHost").Visibility == Visibility.Collapsed,
                "Manual discovery did not open a title-bar-only details page");
            Require(Find<Button>(main, "BtnDownloadUpdate").IsEnabled, "The real compatible package cannot be downloaded");
            var discovered = Field<object>(main, "_availableUpdate");
            foreach (var appearance in new[] { CodexContinue.ThemeMode.Light, CodexContinue.ThemeMode.Dark })
            foreach (var lang in new[] { Loc.Lang.Zh, Loc.Lang.En })
            {
                ThemeManager.SetMode(appearance, Application.Current);
                Loc.Set(lang);
                Invoke(main, "ApplyLoc");
                Pump(120);
                CheckReleaseFooter(main, (UpdateRelease)discovered);
                Capture(root, $"updates-details-{appearance}-{lang}.png");
            }
            Click(main, "BtnUpdateBack");
            Pump(300);
            Require(Find<FrameworkElement>(main, "UpdatePage").Visibility == Visibility.Collapsed &&
                Find<FrameworkElement>(main, "TabRow").Visibility == previousNavigation && Find<FrameworkElement>(main, "SplashPage").Visibility == Visibility.Visible,
                "Back did not return to the startup screen");
            using var emptyHttp = new HttpClient(new Handler((_, _) => Task.FromResult(Json("[]"))));
            using var empty = Client(emptyHttp);
            Set(main, "_updates", empty);
            Wait((Task)Invoke(main, "CheckForUpdatesAsync", true)!);
            Require(Find<TextBlock>(main, "UpdateCheckStatus").Text == Loc.T("updates.latest"), "An up-to-date manual check has no feedback");
            // Opening update details must not stop an armed continuation or lose its draft.
            Find<FrameworkElement>(main, "SplashPage").Visibility = Visibility.Collapsed;
            var pick = Find<ComboBox>(main, "ConvoPick");
            pick.ItemsSource = new[] { new CodexService.RunningThread("update-test", "Update test conversation", false) };
            pick.SelectedIndex = 0;
            Find<TextBox>(main, "BoxPrompt").Text = "Update screen draft";
            Set(main, "_scanTask", Task.CompletedTask);
            Invoke(main, "Arm");
            Invoke(main, "ShowUpdateDetails", discovered);
            Pump(70);
            Click(main, "BtnUpdateBack");
            Pump(70);
            Invoke(main, "ShowUpdateDetails", discovered);
            Pump(300);
            Click(main, "BtnUpdateBack");
            Pump(300);
            Require(Field<bool>(main, "_armed") && Find<TextBox>(main, "BoxPrompt").Text == "Update screen draft",
                "Update/back navigation interrupted continuation or erased the draft");
            Invoke(main, "Disarm", "");
            // A download survives Back, coalesces repeated clicks and leaves the selected mode intact.
            Find<System.Windows.Controls.Primitives.ToggleButton>(main, "BtnSettings").IsChecked = true;
            Pump(500);
            Capture(root, "updates-settings-about.png");
            Invoke(main, "ShowUpdateDetails", discovered);
            Pump(280);
            var gate = new TaskCompletionSource();
            int downloads = 0;
            using var downloadHttp = new HttpClient(new Handler((_, _) =>
            {
                downloads++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new GatedStream(gate.Task)) });
            }));
            using var downloadClient = Client(downloadHttp);
            Set(main, "_updates", downloadClient);
            string downloadRoot = Path.Combine(AppContext.BaseDirectory, "Artifacts", "update-ui-" + Guid.NewGuid().ToString("N"));
            Set(main, "_updateDownloadRoot", downloadRoot);
            try
            {
                var document = Find<FlowDocumentScrollViewer>(main, "UpdateNotes").Document;
                Click(main, "BtnDownloadUpdate");
                Click(main, "BtnDownloadUpdate");
                Pump(240);
                Require(downloads == 1 && !Find<Button>(main, "BtnDownloadUpdate").IsEnabled, "Repeated Download clicks started duplicate transfers");
                Require(Find<TextBlock>(main, "UpdateDownloadStatus").Text.Contains("/"), "Download progress is not visible");
                Require(ReferenceEquals(document, Find<FlowDocumentScrollViewer>(main, "UpdateNotes").Document), "Progress updates reset the release notes");
                Capture(root, "updates-download-progress.png");
                Click(main, "BtnUpdateBack");
                Pump(260);
                Require(Find<FrameworkElement>(main, "SettingsPage").IsVisible && Field<bool>(main, "_updateDownloading"), "Back stopped the download or lost Settings mode");
                gate.SetResult();
                Wait(Field<Task>(main, "_updateDownloadTask"));
                Invoke(main, "ShowUpdateDetails", discovered);
                Pump(280);
                Require(Find<Button>(main, "BtnDownloadUpdate").Content?.ToString() == Loc.T("updates.openFolder"), "Completed download has no Open folder action");
                Require(Find<TextBlock>(main, "UpdateDownloadStatus").Text == Loc.T("updates.downloaded"), "Queued progress overwrote the completion message");
                Capture(root, "updates-download-complete.png");
            }
            finally
            {
                gate.TrySetResult();
                Wait(Field<Task>(main, "_updateDownloadTask"));
                if (Directory.Exists(downloadRoot))
                {
                    foreach (string file in Directory.EnumerateFiles(downloadRoot, "*", SearchOption.AllDirectories)) File.Delete(file);
                    foreach (string folder in Directory.EnumerateDirectories(downloadRoot)) Directory.Delete(folder);
                    Directory.Delete(downloadRoot);
                }
            }
            var longRelease = (UpdateRelease)discovered with { Notes = string.Join("\n\n", Enumerable.Range(1, 30).Select(i => $"## Update {i}\n\nRelease notes paragraph {i}.")) };
            Invoke(main, "ShowUpdateDetails", longRelease);
            Pump(200);
            var scroll = Visual<ScrollViewer>(Find<FlowDocumentScrollViewer>(main, "UpdateNotes"))!;
            scroll.ScrollToEnd();
            Pump(100);
            Require(scroll.VerticalOffset > 0 && scroll.VerticalOffset >= scroll.ScrollableHeight - 1, "Long release notes cannot scroll to the end");
            CheckReleaseFooter(main, longRelease);
            Capture(root, "updates-notes-scroll.png");
            Click(main, "BtnUpdateBack");
            Pump(260);
            using var failedHttp = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden))));
            using var failedClient = Client(failedHttp);
            Set(main, "_updates", failedClient);
            Wait((Task)Invoke(main, "CheckForUpdatesAsync", true)!);
            Require(Find<TextBlock>(main, "UpdateCheckStatus").Text == Loc.T("updates.checkFailed"), "A failed manual check claims the app is current");
        }
        finally
        {
            host.Content = null;
            host.Close();
            main.Content = root;
            Set(main, "_forceClose", true);
            main.Close();
            ThemeManager.SetMode(theme, Application.Current);
            Loc.Set(language);
            SynchronizationContext.SetSynchronizationContext(context);
        }
    }

    public static void IsolateLoadedWindow(MainWindow main)
    {
        var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json("[]"))));
        Set(main, "_updates", Client(http));
    }

    static void CheckReleaseFooter(MainWindow main, UpdateRelease release)
    {
        var date = Find<TextBlock>(main, "UpdateReleaseDate");
        string expected = (Loc.Current == Loc.Lang.Zh ? "发布时间：" : "Published: ") + release.PublishedAt.ToLocalTime().ToString("yyyy-MM-dd");
        Require(date.Text == expected, "The release footer does not show the selected release's full publication date");
        var container = (FrameworkElement)date.Parent;
        var bottomRight = date.TranslatePoint(new Point(date.ActualWidth, date.ActualHeight), container);
        var notes = Find<FlowDocumentScrollViewer>(main, "UpdateNotes");
        var notesBottom = notes.TranslatePoint(new Point(0, notes.ActualHeight), container).Y;
        Require(date.IsVisible && Math.Abs(bottomRight.X - container.ActualWidth) < 2 &&
            Math.Abs(bottomRight.Y - container.ActualHeight) < 2 && notesBottom <= date.TranslatePoint(new Point(), container).Y,
            "The publication date does not stay below the notes in the bottom-right corner");
    }

    static string Release(string tag, bool draft = false, string? hash = null) => JsonSerializer.Serialize(new
    {
        tag_name = tag, name = tag, draft, prerelease = true, published_at = "2026-11-03T01:00:00Z",
        body = "## 更新内容\n\n- **体验优化**\n- 保留所选对话和设置\n\n```text\nBeta release notes\n```\n\n[项目仓库](https://github.com/Belate-stay/Codex-Continue/)",
        assets = new[] {
            new { name = "CodexContinue-linux-x64.zip", browser_download_url = "https://github.com/Belate-stay/Codex-Continue/releases/download/test/linux.zip", size = Package.Length, digest = "sha256:" + (hash ?? Convert.ToHexString(SHA256.HashData(Package))) },
            new { name = "CodexContinue-win-arm64.exe", browser_download_url = "https://github.com/Belate-stay/Codex-Continue/releases/download/test/arm.exe", size = Package.Length, digest = "sha256:" + (hash ?? Convert.ToHexString(SHA256.HashData(Package))) },
            new { name = "Codex-Continue.exe", browser_download_url = "https://github.com/Belate-stay/Codex-Continue/releases/download/test/Codex-Continue.exe", size = Package.Length, digest = "sha256:" + (hash ?? Convert.ToHexString(SHA256.HashData(Package))) }
        }
    });
    static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    static object Version(string value) => VersionType.GetMethod("Parse")!.Invoke(null, new object[] { value })!;
    static IDisposable Client(HttpClient http) => (IDisposable)Activator.CreateInstance(ClientType, http)!;
    static object? Find(IDisposable client, string current)
    {
        var task = (Task)ClientType.GetMethod("FindUpdateAsync")!.Invoke(client, new[] { Version(current), (object)CancellationToken.None })!;
        task.GetAwaiter().GetResult();
        return task.GetType().GetProperty("Result")!.GetValue(task);
    }
    static string Download(IDisposable client, object release, string folder, CancellationToken token = default)
    {
        var task = (Task)ClientType.GetMethod("DownloadAsync")!.Invoke(client, new object?[] { release, folder, null, token })!;
        task.GetAwaiter().GetResult();
        return (string)task.GetType().GetProperty("Result")!.GetValue(task)!;
    }
    static T Property<T>(object value, string name) => (T)(value.GetType().GetProperty(name)?.GetValue(value) ?? throw new Exception("Missing update value " + name));
    static T Find<T>(MainWindow main, string name) where T : class => main.FindName(name) as T ?? throw new Exception("Missing update control " + name);
    static T Field<T>(MainWindow main, string name) => (T)typeof(MainWindow).GetField(name, Private)!.GetValue(main)!;
    static void Set(MainWindow main, string name, object value) => (typeof(MainWindow).GetField(name, Private) ?? throw new Exception("Missing update field " + name)).SetValue(main, value);
    static object? Invoke(MainWindow main, string name, params object[] args) => (typeof(MainWindow).GetMethod(name, Private) ?? throw new Exception("Missing update method " + name)).Invoke(main, args);
    static void Click(MainWindow main, string name) => Find<Button>(main, name).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void RequireThrows(Action action, string message) { try { action(); } catch { return; } throw new Exception(message); }
    static void Wait(Task task)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!task.IsCompleted && DateTime.UtcNow < until) Pump(10);
        Require(task.IsCompleted, "An update action stalled the UI");
        task.GetAwaiter().GetResult();
    }
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    static void Capture(FrameworkElement root, string name)
    {
        var bounds = new Rect(0, 0, root.ActualWidth, root.ActualHeight);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle((Brush)Application.Current.FindResource("CanvasBrush"), null, bounds);
            drawing.DrawRectangle(new VisualBrush(root) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds }, null, bounds);
        }
        var bitmap = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Artifacts"));
        using var output = File.Create(Path.Combine(AppContext.BaseDirectory, "Artifacts", name)); encoder.Save(output);
    }
    static T? Visual<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T found) return found;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (Visual<T>(VisualTreeHelper.GetChild(root, i)) is { } result) return result;
        return null;
    }
    sealed class InterruptedStream : MemoryStream
    {
        public InterruptedStream() : base(Package) { }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
            => Position > 0 ? ValueTask.FromException<int>(new IOException("Interrupted fixture connection")) : base.ReadAsync(buffer[..7], token);
    }
    sealed class GatedStream(Task gate) : MemoryStream(Package)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            if (Position > 0) await gate.WaitAsync(token);
            return await base.ReadAsync(Position == 0 ? buffer[..7] : buffer, token);
        }
    }
    sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
