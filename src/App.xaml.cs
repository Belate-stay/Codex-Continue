using System;
using System.Windows;

namespace CodexContinue;

public partial class App : Application
{
    SingleInstance? _instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instance = new SingleInstance(@"Local\CodexContinue", () =>
        {
            if (!Dispatcher.HasShutdownStarted)
                Dispatcher.BeginInvoke(new Action(() => (MainWindow as CodexContinue.MainWindow)?.RestoreFromTray()));
        });
        if (!_instance.IsPrimary)
        {
            var hwnd = Native.FindWindowW(null, "Codex-Continue");
            if (hwnd != IntPtr.Zero)
            {
                Native.GetWindowThreadProcessId(hwnd, out uint processId);
                Native.AllowSetForegroundWindow(processId);
            }
            _instance.RequestActivation();
            Shutdown();
            return;
        }
        base.OnStartup(e);
        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }
}
