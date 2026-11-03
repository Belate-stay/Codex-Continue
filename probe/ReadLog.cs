using System;
using System.Runtime.InteropServices;
using System.Windows.Automation;

public static class ReadLog
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lp);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);

    public static void Run()
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
        if (found == IntPtr.Zero) { Console.WriteLine("NO_APP_WINDOW"); return; }

        var root = AutomationElement.FromHandle(found);
        var texts = root.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
        foreach (AutomationElement t in texts)
        {
            var n = t.Current.Name;
            if (!string.IsNullOrEmpty(n)) Console.WriteLine("T: " + n.Replace("\n", "\\n"));
        }
    }
}
