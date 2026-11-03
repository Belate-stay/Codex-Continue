Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$p = Get-Process CodexContinue -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Output 'NO WINDOW'; exit 1 }
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)

# switch to manual tab
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '手动定时')
$tab = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
if ($tab) {
  $tab.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
  Write-Output 'clicked manual tab'
  Start-Sleep -Milliseconds 800
} else { Write-Output 'manual tab NOT FOUND' }

function Read-Box($id) {
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
  $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
  if (-not $el) { Write-Output ("$id : NOT FOUND"); return }
  try {
    $tp = $el.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern)
    $v = $tp.DocumentRange.GetText(-1)
    Write-Output ("$id : TEXT [" + $v + "]")
  } catch {
    try {
      $vp = $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
      Write-Output ("$id : VALUE [" + $vp.Current.Value + "]")
    } catch { Write-Output ("$id : no text pattern") }
  }
}
Read-Box 'BoxSendTime'
