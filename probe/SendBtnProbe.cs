using System;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Automation;

// Zero-risk probe: type into composer (no Enter), enumerate buttons that appear
// (looking for a send button), then CLEAR the composer.
public static class SendBtnProbe
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] p, int cb);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lp);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    struct INPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public KEYBDINPUT ki;
    }

    const uint INPUT_KEYBOARD = 1;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const uint KEYEVENTF_UNICODE = 0x0004;

    static void Type(string s)
    {
        foreach (char c in s)
        {
            var down = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = 0, wScan = c, dwFlags = KEYEVENTF_UNICODE } };
            var up = down; up.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
            SendInput(2, new[] { down, up }, Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(25);
        }
    }

    static void Key(ushort vk)
    {
        var down = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = vk } };
        var up = down; up.ki.dwFlags = KEYEVENTF_KEYUP;
        SendInput(2, new[] { down, up }, Marshal.SizeOf(typeof(INPUT)));
    }

    static IntPtr FindChatGptWindow()
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) =>
        {
            if (!IsWindowVisible(h)) return true;
            uint pid; GetWindowThreadProcessId(h, out pid);
            try
            {
                var p = System.Diagnostics.Process.GetProcessById((int)pid);
                if (p.ProcessName.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase)) { found = h; return false; }
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static void Run()
    {
        IntPtr hwnd = FindChatGptWindow();
        if (hwnd == IntPtr.Zero) { Console.WriteLine("NO_WINDOW"); return; }
        var root = AutomationElement.FromHandle(hwnd);

        var edits = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
        AutomationElement composer = null;
        foreach (AutomationElement e in edits)
        {
            if (e.Current.ClassName != null && e.Current.ClassName.Contains("ProseMirror")) { composer = e; break; }
        }
        if (composer == null) { Console.WriteLine("NO_COMPOSER edits=" + edits.Count); return; }

        if (IsIconic(hwnd)) ShowWindow(hwnd, 9);
        uint dummy;
        uint fgTid = GetWindowThreadProcessId(GetForegroundWindow(), out dummy);
        uint myTid = GetCurrentThreadId();
        AttachThreadInput(myTid, fgTid, true);
        SetForegroundWindow(hwnd);
        AttachThreadInput(myTid, fgTid, false);
        Thread.Sleep(250);
        if (GetForegroundWindow() != hwnd) { Console.WriteLine("ABORT: no focus"); return; }

        composer.SetFocus();
        Thread.Sleep(150);

        // snapshot buttons BEFORE typing
        var btnCond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);
        var before = root.FindAll(TreeScope.Descendants, btnCond);
        var beforeNames = new System.Collections.Generic.HashSet<string>();
        foreach (AutomationElement b in before)
            beforeNames.Add(b.Current.Name + "|" + (int)b.Current.BoundingRectangle.X + "," + (int)b.Current.BoundingRectangle.Y);

        Type("probe");
        Thread.Sleep(500);

        // buttons AFTER typing — new ones near the composer are likely send/voice
        var after = root.FindAll(TreeScope.Descendants, btnCond);
        Console.WriteLine("BUTTONS_BEFORE=" + before.Count + " AFTER=" + after.Count);
        foreach (AutomationElement b in after)
        {
            var key = b.Current.Name + "|" + (int)b.Current.BoundingRectangle.X + "," + (int)b.Current.BoundingRectangle.Y;
            if (!beforeNames.Contains(key))
            {
                var r = b.Current.BoundingRectangle;
                string pat = "";
                try { if (b.GetCurrentPattern(InvokePattern.Pattern) != null) pat = "Invoke"; } catch { }
                Console.WriteLine("NEW_BUTTON name='" + b.Current.Name + "' class='" + b.Current.ClassName + "' rect=" + (int)r.X + "," + (int)r.Y + "," + (int)r.Width + "x" + (int)r.Height + " pat=" + pat);
            }
        }

        // also dump ALL buttons whose name is short (likely icon buttons) near composer rect y>1000
        foreach (AutomationElement b in after)
        {
            var r = b.Current.BoundingRectangle;
            if (r.Y > 1000 && b.Current.Name.Length <= 12 && b.Current.Name.Length > 0)
            {
                string pat = "";
                try { if (b.GetCurrentPattern(InvokePattern.Pattern) != null) pat = "Invoke"; } catch { }
                Console.WriteLine("NEAR_COMPOSER_BTN name='" + b.Current.Name + "' rect=" + (int)r.X + "," + (int)r.Y + "," + (int)r.Width + "x" + (int)r.Height + " pat=" + pat);
            }
        }

        // clear
        var downCtrl = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = 0xA2 } };
        var upCtrl = downCtrl; upCtrl.ki.dwFlags = KEYEVENTF_KEYUP;
        SendInput(1, new[] { downCtrl }, Marshal.SizeOf(typeof(INPUT)));
        Key(0x41);
        SendInput(1, new[] { upCtrl }, Marshal.SizeOf(typeof(INPUT)));
        Thread.Sleep(80);
        Key(0x2E);
        Thread.Sleep(250);

        string cleared = "?";
        try
        {
            var vp = composer.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (vp != null) cleared = "'" + vp.Current.Value + "'";
        }
        catch { }
        Console.WriteLine("AFTER_CLEAR: " + cleared);
        Console.WriteLine("DONE");
    }
}
