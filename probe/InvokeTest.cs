using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

// Deterministically invoke the 试发送 button on CodexContinue via UIA (no coordinate guessing),
// then dump the app's text nodes (log rows) to stdout.
public static class InvokeTest
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lp);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);

    static IntPtr FindAppWindow()
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) =>
        {
            if (!IsWindowVisible(h)) return true;
            uint pid; GetWindowThreadProcessId(h, out pid);
            try
            {
                var p = System.Diagnostics.Process.GetProcessById((int)pid);
                if (p.ProcessName.Equals("CodexContinue", StringComparison.OrdinalIgnoreCase)) { found = h; return false; }
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static void Run()
    {
        IntPtr hwnd = FindAppWindow();
        if (hwnd == IntPtr.Zero) { Console.WriteLine("NO_APP_WINDOW"); return; }
        var root = AutomationElement.FromHandle(hwnd);

        AutomationElement testBtn = null;
        var btns = root.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
        foreach (AutomationElement b in btns)
        {
            if ((b.Current.Name ?? "") == "试发送") { testBtn = b; break; }
        }
        if (testBtn == null) { Console.WriteLine("NO_TEST_BUTTON"); return; }
        Console.WriteLine("FOUND_TEST_BUTTON rect=" + testBtn.Current.BoundingRectangle);

        try
        {
            var ip = testBtn.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (ip == null) { Console.WriteLine("NO_INVOKE_PATTERN"); return; }
            ip.Invoke();
            Console.WriteLine("INVOKED");
        }
        catch (Exception e) { Console.WriteLine("INVOKE_FAIL: " + e.Message); return; }

        // send chain runs async; give it time
        Thread.Sleep(7000);

        var texts = root.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
        foreach (AutomationElement t in texts)
        {
            var n = t.Current.Name;
            if (string.IsNullOrEmpty(n)) continue;
            // log rows contain HH:mm:ss timestamps or known keywords; print everything short-ish
            if (n.Length < 200) Console.WriteLine("T: " + n.Replace("\n", "\\n"));
        }
    }
}
