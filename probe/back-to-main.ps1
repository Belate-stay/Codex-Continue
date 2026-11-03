Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, (Get-Process CodexContinue).Id)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { Write-Output 'NO WIN'; exit 1 }
# select light theme radio
$radioCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'ThemeLight')
$radio = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $radioCond)
if ($radio) {
  $sel = $radio.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
  $sel.Select()
  Write-Output 'light selected'
} else { Write-Output 'no ThemeLight' }
Start-Sleep -Milliseconds 500
# invoke back button
$backCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'BtnBack')
$back = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $backCond)
if ($back) {
  $inv = $back.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
  $inv.Invoke()
  Write-Output 'back invoked'
} else { Write-Output 'no BtnBack' }
