# Read-only: dump CodexContinue app UI text (log rows) + Codex composer state
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$out = New-Object System.Collections.Generic.List[string]

$proc = Get-Process CodexContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($proc) {
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
    $texts = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))
    $out.Add("=== APP TEXTS ($($texts.Count)) ===")
    foreach ($t in $texts) {
        $n = $t.Current.Name
        if ($n -and $n.Length -lt 220) { $out.Add("T: " + $n.Replace("`n", '\n')) }
    }
} else { $out.Add("NO_APP") }

$chat = Get-Process ChatGPT -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($chat) {
    $croot = [System.Windows.Automation.AutomationElement]::FromHandle($chat.MainWindowHandle)
    $edits = $croot.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))
    foreach ($e in $edits) {
        if ($e.Current.ClassName -like '*ProseMirror*') {
            $val = ''
            try { $vp = $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern); $val = $vp.Current.Value } catch {}
            $out.Add("=== COMPOSER ===")
            $out.Add("VALUE: '" + $val.Replace("`n", '\n') + "'")
        }
    }
    # live banner check
    $bars = $croot.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::StatusBar)))
    $out.Add("=== STATUSBARS: $($bars.Count) ===")
    foreach ($b in $bars) {
        $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
        $child = $walker.GetFirstChild($b)
        while ($child) {
            $n = $child.Current.Name
            if ($n -and $n.Length -lt 200) { $out.Add("BAR: " + $n) }
            $child = $walker.GetNextSibling($child)
        }
    }
} else { $out.Add("NO_CHATGPT") }

[IO.File]::WriteAllLines('D:\Desktop\Codex-Continue\probe\readonly-out.txt', $out, (New-Object System.Text.UTF8Encoding($false)))
Write-Output "WROTE $($out.Count)"
