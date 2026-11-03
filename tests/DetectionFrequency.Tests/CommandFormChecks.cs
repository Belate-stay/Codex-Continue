using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodexContinue;

static class CommandFormChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run(MainWindow main)
    {
        var prompt = (TextBox)main.FindName("BoxPrompt");
        var combo = (ComboBox)main.FindName("ConvoPick");
        var run = (Button)main.FindName("BtnRun");
        string originalText = prompt.Text;
        var originalThreads = Field<List<CodexService.RunningThread>>(main, "_threads");
        try
        {
            Require(prompt.Text == "/Continue", "The initial command is not /Continue");
            Invoke(main, "ApplyLoc");
            var first = new CodexService.RunningThread("test-a", "First", true);
            var chosen = new CodexService.RunningThread("test-b", "Chosen", false);
            SetThreads(main, first, chosen);
            Require(combo.SelectedIndex == -1 && Field<string?>(main, "_targetThreadId") == null,
                "A scan automatically chooses a conversation");
            Require(!run.IsEnabled, "Run is enabled without an explicit conversation");
            Require(ToolTipService.GetShowOnDisabled(run) && Equals(run.ToolTip, Loc.T("cmd.incomplete")),
                "The disabled Run button cannot show the incomplete-fields hint");
            combo.ApplyTemplate();
            var placeholder = combo.Template.FindName("ConvoPlaceholder", combo) as TextBlock;
            Require(placeholder?.Visibility == Visibility.Visible, "The empty conversation field lacks a selection hint");
            Invoke(main, "Arm");
            Require(!Field<bool>(main, "_armed"), "Arming bypasses a missing conversation");

            combo.SelectedIndex = 1;
            Require(run.IsEnabled && Field<string?>(main, "_targetThreadId") == chosen.Id,
                "An explicit conversation with the default prompt cannot run");
            foreach (string empty in new[] { "", " \t\r\n" })
            {
                prompt.Text = empty;
                Require(!run.IsEnabled, "An empty or whitespace-only prompt enables Run");
                Invoke(main, "Arm");
                Require(!Field<bool>(main, "_armed"), "Arming bypasses an empty prompt");
            }
            Field<List<string>>(main, "_attachments").Add("test-attachment.txt");
            Require(!run.IsEnabled, "An attachment substitutes for a required prompt");
            Field<List<string>>(main, "_attachments").Clear();
            prompt.Text = "Custom continuation";
            Require(run.IsEnabled, "A nonempty custom prompt does not enable Run");
            SetThreads(main, chosen with { Name = "Renamed" }, first);
            Require(combo.SelectedIndex == 0 && Field<string?>(main, "_targetThreadId") == chosen.Id,
                "Refreshing a reordered list loses the exact conversation the user chose");
            SetThreads(main, new CodexService.RunningThread("replacement", "Renamed", true));
            Require(combo.SelectedIndex == -1 && !run.IsEnabled && Field<string?>(main, "_targetThreadId") == null,
                "A missing target silently falls back to a matching name or running conversation");

            SetThreads(main, chosen);
            combo.SelectedIndex = 0;
            prompt.Text = "/Continue";
            Invoke(main, "Arm");
            Require(Field<bool>(main, "_armed") && run.IsEnabled, "A complete command cannot arm");
            SetThreads(main);
            prompt.Text = ""; // A running form is read-only; simulate a refresh invalidation.
            Require(run.IsEnabled && Field<string?>(main, "_targetThreadId") == chosen.Id,
                "A missing scan result disables Stop or replaces the armed target");
            Invoke(main, "Disarm", "test stopped");
            Require(!run.IsEnabled && Field<string?>(main, "_targetThreadId") == null,
                "Stopping does not restore incomplete-form validation");
        }
        finally
        {
            if (Field<bool>(main, "_armed")) Invoke(main, "Disarm", "test stopped");
            Field<List<string>>(main, "_attachments").Clear();
            prompt.Text = originalText;
            typeof(MainWindow).GetField("_threads", Private)!.SetValue(main, originalThreads);
            Invoke(main, "RefreshTargetThread");
        }
    }

    public static void Tooltips(MainWindow main)
    {
        var style = Application.Current.TryFindResource(typeof(ToolTip)) as Style;
        Require(style != null, "The shared rounded ToolTip style is missing");
        var run = (Button)main.FindName("BtnRun");
        var theme = ThemeManager.Mode;
        var language = Loc.Current;
        try
        {
            foreach (var mode in new[] { CodexContinue.ThemeMode.Light, CodexContinue.ThemeMode.Dark })
            {
                ThemeManager.SetMode(mode, Application.Current);
                run.ApplyTemplate();
                var face = (Border)run.Template.FindName("bd", run);
                var expected = ((SolidColorBrush)Application.Current.FindResource("SurfacePressedBrush")).Color;
                Require(face.Background is SolidColorBrush ink && ink.Color == expected,
                    "The disabled Run key still has its active blue face");
                var tip = new ToolTip { Content = "请查看内容填写是否完整", Style = style };
                tip.ApplyTemplate();
                var outline = tip.Template.FindName("ToolTipFace", tip) as Border;
                Require(outline != null && outline.CornerRadius.TopLeft == 8 && outline.CornerRadius.BottomRight == 8,
                    "The shared hover hint is still rectangular");
                expected = ((SolidColorBrush)Application.Current.FindResource("PillBrush")).Color;
                Require(outline!.Background is SolidColorBrush paper && paper.Color == expected,
                    "The hover hint ignores the current theme");
                tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                tip.Arrange(new Rect(tip.DesiredSize));
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(tip.ActualWidth), (int)Math.Ceiling(tip.ActualHeight),
                    96, 96, PixelFormats.Pbgra32);
                bitmap.Render(tip);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                string directory = Path.Combine(AppContext.BaseDirectory, "Artifacts");
                Directory.CreateDirectory(directory);
                using var output = File.Create(Path.Combine(directory, $"tooltip-{mode}.png"));
                encoder.Save(output);
            }
            Loc.Set(Loc.Lang.Zh);
            Invoke(main, "ApplyLoc");
            Require(Equals(run.ToolTip, "请查看内容填写是否完整"), "The Chinese incomplete hint differs from the requested text");
            Require(Equals(((Button)main.FindName("BtnRecheck")).ToolTip, "检测Codex连接与额度"),
                "The detection button lacks its requested hint");
            Loc.Set(Loc.Lang.En);
            Invoke(main, "ApplyLoc");
            Require(Equals(run.ToolTip, Loc.T("cmd.incomplete")) && !Equals(run.ToolTip, "请查看内容填写是否完整"),
                "The incomplete hint does not follow the selected language");
            Require(Equals(((Button)main.FindName("BtnRecheck")).ToolTip, Loc.T("cmd.recheckHint")),
                "The detection hint does not follow the selected language");
        }
        finally
        {
            Loc.Set(language);
            ThemeManager.SetMode(theme, Application.Current);
            Invoke(main, "ApplyLoc");
        }
    }

    static void SetThreads(MainWindow main, params CodexService.RunningThread[] threads)
    {
        typeof(MainWindow).GetField("_threads", Private)!.SetValue(main, threads.ToList());
        Invoke(main, "RefreshTargetThread");
    }

    static T Field<T>(MainWindow main, string name) => (T)typeof(MainWindow).GetField(name, Private)!.GetValue(main)!;
    static object? Invoke(MainWindow main, string name, params object[] args)
        => typeof(MainWindow).GetMethod(name, Private)!.Invoke(main, args);
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
