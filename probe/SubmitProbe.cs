using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

// Diagnose WHY submit fails while quota is exhausted:
// 1) IsEnabled state of composer + send button
// 2) Try Enter via scancode (not VK)
// 3) Try send-button Invoke
// Then report whether the composer drains. Leaves composer EMPTY at the end.
public static class SubmitProbe
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lp);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] p, int cb);
    [DllImport("user32.dll")] static extern uint MapVirtualKeyW(uint code, uint type);

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    struct INPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public KEYBDINPUT ki;
    }

    const uint KEYEVENTF_KEYUP = 0x0002;
    const uint KEYEVENTF_UNICODE = 0x0004;
    const uint KEYEVENTF_SCANCODE = 0x0008;

    static IntPtr FindWindowByProc(string name)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) =>
        {
            if (!IsWindowVisible(h)) return true;
            uint pid; GetWindowThreadProcessId(h, out pid);
            try
            {
                var p = System.Diagnostics.Process.GetProcessById((int)pid);
                if (p.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase)) { found = h; return false; }
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    static void EnterVk()
    {
        var down = new INPUT { type = 1, ki = new KEYBDINPUT { wVk = 0x0D, wScan = (ushort)MapVirtualKeyW(0x0D, 0) } };
        var up = down; up.ki.dwFlags = KEYEVENTF_KEYUP;
        SendInput(2, new[] { down, up }, Marshal.SizeOf(typeof(INPUT)));
    }

    static void EnterScan()
    {
        var down = new INPUT { type = 1, ki = new KEYBDINPUT { wScan = 0x1C, dwFlags = KEYEVENTF_SCANCODE } };
        var up = down; up.ki.dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP;
        SendInput(2, new[] { down, up }, Marshal.SizeOf(typeof(INPUT)));
    }

    static void Key(ushort vk, bool down)
    {
        var inp = new INPUT { type = 1, ki = new KEYBDINPUT { wVk = vk, dwFlags = down ? 0u : KEYEVENTF_KEYUP } };
        SendInput(1, new[] { inp }, Marshal.SizeOf(typeof(INPUT)));
    }

    static void Key(ushort vk)
    {
        Key(vk, true);
        Key(vk, false);
    }

    static void Type(string s)
    {
        foreach (char c in s)
        {
            var down = new INPUT { type = 1, ki = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE } };
            var up = down; up.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
            SendInput(2, new[] { down, up }, Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(25);
        }
    }

    static string ReadComposer(AutomationElement composer)
    {
        try
        {
            var tp = composer.GetCurrentPattern(TextPattern.Pattern) as TextPattern;
            if (tp != null) return tp.DocumentRange.GetText(-1) ?? "";
        }
        catch { }
        return "?";
    }

    public static void Run()
    {
        IntPtr hwnd = FindWindowByProc("ChatGPT");
        if (hwnd == IntPtr.Zero) { Console.WriteLine("NO_CODEX"); return; }
        var root = AutomationElement.FromHandle(hwnd);

        AutomationElement composer = null;
        var edits = root.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
        foreach (AutomationElement e in edits)
        {
            if ((e.Current.ClassName ?? "").Contains("ProseMirror")) { composer = e; break; }
        }
        if (composer == null) { Console.WriteLine("NO_COMPOSER"); return; }

        Console.WriteLine("COMPOSER_ENABLED=" + composer.Current.IsEnabled);

        // find send button
        AutomationElement sendBtn = null;
        double cx = composer.Current.BoundingRectangle.X + composer.Current.BoundingRectangle.Width;
        double cy = composer.Current.BoundingRectangle.Y + composer.Current.BoundingRectangle.Height / 2;
        double best = double.MaxValue;
        var btns = root.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
        foreach (AutomationElement b in btns)
        {
            var nm = b.Current.Name ?? "";
            if (nm != "发送" && !nm.Equals("Send", StringComparison.OrdinalIgnoreCase)) continue;
            var r = b.Current.BoundingRectangle;
            double dx = r.X - cx, dy = r.Y + r.Height / 2 - cy;
            double d = dx * dx + dy * dy;
            if (d < best) { best = d; sendBtn = b; }
        }
        if (sendBtn == null) { Console.WriteLine("NO_SEND_BTN"); }
        else
        {
            Console.WriteLine("SENDBTN_ENABLED=" + sendBtn.Current.IsEnabled
                + " name='" + sendBtn.Current.Name + "' rect=" + sendBtn.Current.BoundingRectangle);
            Console.WriteLine("SENDBTN_HELPTEXT='" + sendBtn.Current.HelpText + "' AUTOMATIONID='" + sendBtn.Current.AutomationId + "'");
        }

        // focus composer
        uint dummy;
        uint fgTid = GetWindowThreadProcessId(GetForegroundWindow(), out dummy);
        uint myTid = GetCurrentThreadId();
        AttachThreadInput(myTid, fgTid, true);
        SetForegroundWindow(hwnd);
        AttachThreadInput(myTid, fgTid, false);
        Thread.Sleep(250);
        try { composer.SetFocus(); } catch { }
        Thread.Sleep(150);

        Console.WriteLine("COMPOSER_NOW='" + ReadComposer(composer).Replace("\n", "\\n") + "'");

        // ensure empty first
        Key(0xA2, true); Key(0x41); Key(0xA2, false);
        Thread.Sleep(80); Key(0x2E);
        Thread.Sleep(200);
        Console.WriteLine("AFTER_CLEAR='" + ReadComposer(composer).Replace("\n", "\\n") + "'");

        // type a probe message
        Type("probe-submit");
        Thread.Sleep(300);
        Console.WriteLine("AFTER_TYPE='" + ReadComposer(composer).Replace("\n", "\\n") + "'");
        Console.WriteLine("SENDBTN_ENABLED_AFTER_TYPE=" + (sendBtn != null ? sendBtn.Current.IsEnabled.ToString() : "n/a"));

        // attempt 1: Enter VK
        EnterVk();
        Thread.Sleep(600);
        string s1 = ReadComposer(composer);
        Console.WriteLine("AFTER_ENTER_VK='" + s1.Replace("\n", "\\n") + "' drained=" + (!s1.Contains("probe-submit")));

        // attempt 2: Enter scancode (if still there)
        if (s1.Contains("probe-submit"))
        {
            EnterScan();
            Thread.Sleep(600);
            string s2 = ReadComposer(composer);
            Console.WriteLine("AFTER_ENTER_SCAN='" + s2.Replace("\n", "\\n") + "' drained=" + (!s2.Contains("probe-submit")));
            s1 = s2;
        }

        // attempt 3: Invoke send button (if still there)
        if (s1.Contains("probe-submit") && sendBtn != null)
        {
            try
            {
                var ip = sendBtn.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                if (ip != null)
                {
                    ip.Invoke();
                    Thread.Sleep(600);
                    string s3 = ReadComposer(composer);
                    Console.WriteLine("AFTER_INVOKE='" + s3.Replace("\n", "\\n") + "' drained=" + (!s3.Contains("probe-submit")));
                    s1 = s3;
                }
            }
            catch (Exception e) { Console.WriteLine("INVOKE_EX: " + e.Message); }
        }

        // attempt 4: click at send button coords via SendInput mouse? skip — leave clean instead
        if (s1.Contains("probe-submit"))
        {
            Console.WriteLine("SUBMIT_FAILED_ALL — clearing composer to leave clean state");
            Key(0xA2, true); Key(0x41); Key(0xA2, false);
            Thread.Sleep(80); Key(0x2E);
            Thread.Sleep(200);
            Console.WriteLine("FINAL='" + ReadComposer(composer).Replace("\n", "\\n") + "'");
        }
        else
        {
            Console.WriteLine("SUBMIT_OK — a message may now exist in the Codex conversation");
        }
    }
}
