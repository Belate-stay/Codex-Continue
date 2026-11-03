Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class W {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Rt, B; }
}
"@
[W]::SetProcessDPIAware() | Out-Null

function Snap($path) {
  $p = Get-Process CodexContinue -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  if (-not $p) { Write-Output 'NO WINDOW'; return }
  [W]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
  Start-Sleep -Milliseconds 500
  $r = New-Object W+R
  [W]::GetClientRect($p.MainWindowHandle, [ref]$r) | Out-Null
  $w = $r.Rt - $r.L
  $h = $r.B - $r.T
  $bmp = New-Object System.Drawing.Bitmap($w, $h)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc()
  [W]::PrintWindow($p.MainWindowHandle, $hdc, 2) | Out-Null
  $g.ReleaseHdc($hdc)
  $g.Dispose()
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  Write-Output "saved $path"
}


function Click-ById($id) {
  $p = Get-Process CodexContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
  $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
  if (-not $el) { Write-Output "NOT FOUND id: $id"; return }
  $ip = $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
  $ip.Invoke()
  Write-Output "clicked id $id"
}

function Click-Button($name) {
  $p = Get-Process CodexContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
  $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
  if (-not $el) { Write-Output "NOT FOUND: $name"; return $false }
  $ip = $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
  $ip.Invoke()
  Write-Output "clicked $name"
  return $true
}

$out = 'D:/Desktop/Codex-Continue/probe/'
Click-Button '手动定时' | Out-Null
Start-Sleep -Milliseconds 900
Snap ($out + 'ui-v2-manual.png')
$box = $null
{
  $p2 = Get-Process CodexContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  $root2 = [System.Windows.Automation.AutomationElement]::FromHandle($p2.MainWindowHandle)
  $cond2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'BoxSendTime')
  $box = $root2.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond2)
} 2>$null
if ($box) {
  $vp = $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
  Write-Output ('BoxSendTime value: [' + $vp.Current.Value + ']')
} else { Write-Output 'BoxSendTime not found' }
Click-Button '自动续跑' | Out-Null
Start-Sleep -Milliseconds 400
# settings via the gear button (name may be 设置 tooltip-less; find by class button order)
Click-ById 'BtnSettings' | Out-Null
Start-Sleep -Milliseconds 600
Snap ($out + 'ui-v2-settings.png')
Click-ById 'BtnBack' | Out-Null
Start-Sleep -Milliseconds 400
Click-ById 'BtnLog' | Out-Null
Start-Sleep -Milliseconds 800
Snap ($out + 'ui-v2-logs.png')
Click-Button '清空' | Out-Null
