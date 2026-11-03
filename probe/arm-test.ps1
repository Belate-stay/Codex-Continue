# Arm the guard via UIA, wait for a scan cycle, read back the log
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$out = New-Object System.Collections.Generic.List[string]
$proc = Get-Process CodexContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { $out.Add("NO_APP"); [IO.File]::WriteAllLines('D:\Desktop\Codex-Continue\probe\armtest-out.txt', $out); exit }
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)

function FindByName($name, $type) {
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)),
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type)))
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

# arm via BtnArm (invoke)
$arm = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, "BtnArm")))
if ($arm) {
    $arm.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $out.Add("ARM invoked")
} else { $out.Add("ARM not found") }

Start-Sleep -Seconds 6  # let a scan cycle run

# read log rows + status
$texts = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))
foreach ($t in $texts) {
    $n = $t.Current.Name
    if ($n -and $n.Length -lt 120 -and ($n -match '守护|发送|检测|额度|恢复|提醒|就绪|0[0-9]:')) { $out.Add("T: " + $n) }
}

# disarm again so we don't leave a surprise armed state? No — user will test tonight; leave armed is risky. Disarm.
Start-Sleep -Seconds 1
if ($arm) {
    $arm.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $out.Add("DISARM invoked")
}

[IO.File]::WriteAllLines('D:\Desktop\Codex-Continue\probe\armtest-out.txt', $out, (New-Object System.Text.UTF8Encoding($false)))
Write-Output "WROTE $($out.Count)"
