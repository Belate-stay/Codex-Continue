using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

public static class WinEnum
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lp);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr hWnd, StringBuilder s, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassNameW(IntPtr hWnd, StringBuilder s, int max);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    public static void Dump()
    {
        EnumWindows((h, l) =>
        {
            if (!IsWindowVisible(h)) return true;
            var sb = new StringBuilder(512);
            GetWindowTextW(h, sb, 512);
            string title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title)) return true;
            var sb2 = new StringBuilder(256);
            GetClassNameW(h, sb2, 256);
            string cls = sb2.ToString();
            uint pid; GetWindowThreadProcessId(h, out pid);
            string pn = "?";
            try { pn = Process.GetProcessById((int)pid).ProcessName; } catch { }
            Console.WriteLine("HWND=0x{0:X8} PID={1} PROC={2} CLASS={3} TITLE={4}", h.ToInt64(), pid, pn, cls, title);
            return true;
        }, IntPtr.Zero);
    }
}
