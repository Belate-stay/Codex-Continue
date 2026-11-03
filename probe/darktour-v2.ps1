Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class W3 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Rt, B; }
}
"@
[W3]::SetProcessDPIAware() | Out-Null

function Snap($path) {
  $p = Get-Process CodexContinue -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  if (-not $p) { Write-Output 'NO WINDOW'; return }
  [W3]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
  Start-Sleep -Milliseconds 500
  $r = New-Object W3+R
  [W3]::GetClientRect($p.MainWindowHandle, [ref]$r) | Out-Null
  $w = $r.Rt - $r.L
  $h = $r.B - $r.T
  $bmp = New-Object System.Drawing.Bitmap($w, $h)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc()
  [W3]::PrintWindow($p.MainWindowHandle, $hdc, 2) | Out-Null
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
  try {
    $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
  } catch {
    $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
  }
  Write-Output ("clicked id " + $id)
}

$out = 'D:/Desktop/Codex-Continue/probe/'
Click-ById 'BtnRun'      # arm -> generates log lines
Start-Sleep -Milliseconds 900
Click-ById 'BtnRun'      # same button is now Stop
Start-Sleep -Milliseconds 900
Click-ById 'BtnSettings'
Start-Sleep -Milliseconds 600
Click-ById 'ThemeDark'
Start-Sleep -Milliseconds 700
Click-ById 'BtnBack'
Start-Sleep -Milliseconds 600
Click-ById 'BtnLog'
Start-Sleep -Milliseconds 900
Snap ($out + 'ui-v2-dark.png')
Click-ById 'BtnClearLog'
Start-Sleep -Milliseconds 300
