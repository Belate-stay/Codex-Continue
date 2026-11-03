using System;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Automation;

// Probe: locate ProseMirror composer in ChatGPT/Codex window, try text injection paths,
// verify read-back, then CLEAR the composer. Never presses Enter.
public static class InjectProbe
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] p, int cb);

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
            var down = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = 0, dwFlags = KEYEVENTF_UNICODE, dwExtraInfo = IntPtr.Zero } };
            var up = down; up.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
            down.ki.wScan = c; up.ki.wScan = c;
            SendInput(2, new[] { down, up }, Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(30);
        }
    }

    static void Key(ushort vk)
    {
        var down = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = vk } };
        var up = down; up.ki.dwFlags = KEYEVENTF_KEYUP;
        SendInput(2, new[] { down, up }, Marshal.SizeOf(typeof(INPUT)));
    }

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lp);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);

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
                if (p.ProcessName.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase))
                {
                    found = h;
                    return false;
                }
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
        Console.WriteLine("ROOT: hwnd=0x" + hwnd.ToString("X8") + " name='" + root.Current.Name + "' pid=" + root.Current.ProcessId);

        // find composer: Edit with class containing ProseMirror
        var cond = new PropertyCondition(AutomationElement.ClassNameProperty, "ProseMirror");
        var edits = root.FindAll(TreeScope.Descendants, cond);
        Console.WriteLine("PROSEMIRROR_EDITS=" + edits.Count);
        AutomationElement composer = edits.Count > 0 ? edits[0] : null;
        if (composer == null) { Console.WriteLine("NO_COMPOSER"); return; }

        Console.WriteLine("COMPOSER name='" + composer.Current.Name + "' class='" + composer.Current.ClassName + "'");

        // 1) Try ValuePattern.SetValue (no focus needed)
        bool valueWorked = false;
        try
        {
            var vp = composer.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (vp != null)
            {
                Console.WriteLine("VALUE_BEFORE='" + vp.Current.Value + "'");
                vp.SetValue("【测试勿回】probe-1");
                Thread.Sleep(300);
                string after = vp.Current.Value;
                Console.WriteLine("VALUE_AFTER_SETVALUE='" + after + "'");
                valueWorked = after.Contains("probe-1");
            }
            else Console.WriteLine("NO_VALUE_PATTERN");
        }
        catch (Exception e) { Console.WriteLine("SETVALUE_EX: " + e.Message); }

        // clear whatever is there
        try
        {
            var vp = composer.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (vp != null && !string.IsNullOrEmpty(vp.Current.Value)) { vp.SetValue(""); Thread.Sleep(150); }
        }
        catch { }

        // 2) Focus + SendInput unicode typing
        if (IsIconic(hwnd)) ShowWindow(hwnd, 9);
        uint fgTidDummy;
        uint fgTid = GetWindowThreadProcessId(GetForegroundWindow(), out fgTidDummy);
        uint myTid = GetCurrentThreadId();
        bool attached = AttachThreadInput(myTid, fgTid, true);
        SetForegroundWindow(hwnd);
        AttachThreadInput(myTid, fgTid, false);
        Thread.Sleep(250);
        Console.WriteLine("FOREGROUND_NOW=0x" + GetForegroundWindow().ToString("X8") + " (want 0x208BC)");

        composer.SetFocus();
        Thread.Sleep(200);

        // Safety gate: only type if the ChatGPT window really has focus
        IntPtr fg = GetForegroundWindow();
        if (fg != hwnd)
        {
            Console.WriteLine("ABORT_TYPE: foreground=0x" + fg.ToString("X8") + " != target. Not typing.");
            Console.WriteLine("RESULT: setValue=" + (valueWorked ? "WORKS" : "FAILS") + " sendInputType=SKIPPED");
            return;
        }

        Type("【测试勿回】probe-2");
        Thread.Sleep(400);

        // read back
        string rb = "?";
        try
        {
            var vp = composer.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (vp != null) rb = "value='" + vp.Current.Value + "'";
        }
        catch { }
        try
        {
            var tp = composer.GetCurrentPattern(TextPattern.Pattern) as TextPattern;
            if (tp != null) rb += " text='" + tp.DocumentRange.GetText(-1).Replace("\r", "\\r").Replace("\n", "\\n") + "'";
        }
        catch { }
        Console.WriteLine("READBACK_AFTER_TYPE: " + rb);
        bool typeWorked = rb.Contains("probe-2");

        // 3) CLEAR composer without Enter: Ctrl+A then Delete
        var downCtrl = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = 0xA2 } };
        var upCtrl = downCtrl; upCtrl.ki.dwFlags = KEYEVENTF_KEYUP;
        SendInput(1, new[] { downCtrl }, Marshal.SizeOf(typeof(INPUT)));
        Key(0x41); // A
        SendInput(1, new[] { upCtrl }, Marshal.SizeOf(typeof(INPUT)));
        Thread.Sleep(80);
        Key(0x2E); // Delete
        Thread.Sleep(250);

        string cleared = "?";
        try
        {
            var vp = composer.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (vp != null) cleared = "'" + vp.Current.Value + "'";
        }
        catch { }
        Console.WriteLine("AFTER_CLEAR: " + cleared);

        Console.WriteLine("RESULT: setValue=" + (valueWorked ? "WORKS" : "FAILS") + " sendInputType=" + (typeWorked ? "WORKS" : "FAILS"));
    }
}
