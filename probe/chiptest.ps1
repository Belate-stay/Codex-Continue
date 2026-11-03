Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class W4 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Rt, B; }
}
"@
[W4]::SetProcessDPIAware() | Out-Null

function Snap($path) {
  $p = Get-Process CodexContinue -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  if (-not $p) { Write-Output 'NO WINDOW'; return }
  [W4]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
  Start-Sleep -Milliseconds 500
  $r = New-Object W4+R
  [W4]::GetClientRect($p.MainWindowHandle, [ref]$r) | Out-Null
  $w = $r.Rt - $r.L
  $h = $r.B - $r.T
  $bmp = New-Object System.Drawing.Bitmap($w, $h)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc()
  [W4]::PrintWindow($p.MainWindowHandle, $hdc, 2) | Out-Null
  $g.ReleaseHdc($hdc)
  $g.Dispose()
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  Write-Output ("saved " + $path)
}

function Find-ById($id) {
  $p = Get-Process CodexContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
  return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Click-ById($id) {
  $el = Find-ById $id
  if (-not $el) { Write-Output ("NOT FOUND id: " + $id); return }
  try { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
  catch { $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
  Write-Output ("clicked id " + $id)
}

$out = 'D:/Desktop/Codex-Continue/probe/'
Click-ById 'BtnAttach'
Start-Sleep -Milliseconds 1200

# drive the native file dialog: find its File-name edit and Open button
$dlg = Get-Process CodexContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
# the dialog belongs to the same process; enumerate top-level windows via UIA with class #32770
$root = [System.Windows.Automation.AutomationElement]::RootElement
$procCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $dlg.Id)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $procCond)
$fileEdit = $null
foreach ($w in $wins) {
  if ($w.Current.ClassName -ne '#32770') { continue }
  Write-Output ("dialog: " + $w.Current.Name)
  $eCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, '1148')
  $fileEdit = $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $eCond)
  if (-not $fileEdit) {
    $cCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ClassNameProperty, 'Edit')
    $fileEdit = $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cCond)
  }
  if ($fileEdit) {
    # two files separated by a space, quoted paths
    $paths = 'D:\Desktop\Prompt.txt D:\Desktop\Codex-Continue\src\app.ico'
    $fileEdit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($paths)
    Write-Output 'typed file paths'
    # press Enter via the Open button (name is locale-dependent; try id 1)
    $btnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, '1')
    $openBtn = $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
    if ($openBtn) {
      $openBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
      Write-Output 'clicked Open'
    }
    break
  }
}
Start-Sleep -Milliseconds 1000
Snap ($out + 'ui-v2-chips.png')
