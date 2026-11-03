Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class W2 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Rt, B; }
}
"@
[W2]::SetProcessDPIAware() | Out-Null

function Snap($path) {
  $p = Get-Process CodexContinue -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  if (-not $p) { Write-Output 'NO WINDOW'; return }
  [W2]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
  Start-Sleep -Milliseconds 500
  $r = New-Object W2+R
  [W2]::GetClientRect($p.MainWindowHandle, [ref]$r) | Out-Null
  $w = $r.Rt - $r.L
  $h = $r.B - $r.T
  $bmp = New-Object System.Drawing.Bitmap($w, $h)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc()
  [W2]::PrintWindow($p.MainWindowHandle, $hdc, 2) | Out-Null
  $g.ReleaseHdc($hdc)
  $g.Dispose()
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  Write-Output "saved $path"
}

function Click-ByName($name) {
  $p = Get-Process CodexContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
  $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
  if (-not $el) { Write-Output "NOT FOUND: $name"; return }
  $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
  Write-Output "clicked $name"
}

function Set-Edit($id, $text) {
  $p = Get-Process CodexContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
  $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
  if (-not $el) { Write-Output "edit NOT FOUND: $id"; return }
  $vp = $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
  $vp.SetValue($text)
  Write-Output "typed into $id"
}

$out = 'D:/Desktop/Codex-Continue/probe/'
Start-Sleep -Seconds 2
Set-Edit 'BoxPrompt' '/continue'
Start-Sleep -Milliseconds 300
Click-ByName '运行'
Start-Sleep -Milliseconds 1200
Snap ($out + 'ui-v2-armed.png')
# trigger close -> confirm dialog
Click-ByName '关闭 Codex-Continue' 2>$null
$p = Get-Process CodexContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
# close button name is the glyph; find by automation id fallback: send WM_CLOSE via the button with Name E8BB? use window Close pattern
$winc = $root.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
$winc.Close()
Start-Sleep -Milliseconds 800
Snap ($out + 'ui-v2-closedlg.png')
# cancel the dialog so the app survives
Click-ByName '取消'
Start-Sleep -Milliseconds 500
Click-ByName '停止'
Start-Sleep -Milliseconds 500
Snap ($out + 'ui-v2-stopped.png')
