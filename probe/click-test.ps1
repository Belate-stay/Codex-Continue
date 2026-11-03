# Invoke the 试发送 button on CodexContinue via UIA, then dump the app's text (log rows).
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$out = New-Object System.Collections.Generic.List[string]
$proc = Get-Process CodexContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { $out.Add("NO_APP_WINDOW"); [IO.File]::WriteAllLines('D:\Desktop\Codex-Continue\probe\click-out.txt', $out); exit }
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)

$btns = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
$target = $null
foreach ($b in $btns) {
    if ($b.Current.Name -eq '试发送') { $target = $b; break }
}
if (-not $target) {
    $out.Add("NO_TEST_BUTTON (total=$($btns.Count))")
    foreach ($b in $btns) { $out.Add("BTN: '$($b.Current.Name)' en=$($b.Current.IsEnabled)") }
    [IO.File]::WriteAllLines('D:\Desktop\Codex-Continue\probe\click-out.txt', $out); exit
}
$out.Add("FOUND_TEST_BUTTON rect=$($target.Current.BoundingRectangle)")
$pat = $target.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
$pat.Invoke()
$out.Add("INVOKED")
Start-Sleep -Seconds 9

$texts = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))
foreach ($t in $texts) {
    $n = $t.Current.Name
    if ($n -and $n.Length -lt 220) { $out.Add("T: " + $n.Replace("`n", '\n')) }
}
[IO.File]::WriteAllLines('D:\Desktop\Codex-Continue\probe\click-out.txt', $out, (New-Object System.Text.UTF8Encoding($false)))
Write-Output "WROTE $($out.Count)"
