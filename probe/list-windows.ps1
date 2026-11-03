# List top-level windows with titles + process names (UTF-8 out)
Add-Type @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class WinEnum {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
  public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lp);
  [DllImport("user32.dll")] public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder s, int max);
  [DllImport("user32.dll")] public static extern int GetClassNameW(IntPtr hWnd, StringBuilder s, int max);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
}
"@
$sb = New-Object System.Text.StringBuilder 512
$cb = [WinEnum+EnumWindowsProc]{
  param($h, $lp)
  if (-not [WinEnum]::IsWindowVisible($h)) { return $true }
  $sb.Capacity = 512; [void][WinEnum]::GetWindowTextW($h, $sb, 512)
  $title = $sb.ToString()
  if ([string]::IsNullOrWhiteSpace($title)) { return $true }
  $sb.Capacity = 256; [void][WinEnum]::GetClassNameW($h, $sb, 256)
  $cls = $sb.ToString()
  $pid2 = 0; [void][WinEnum]::GetWindowThreadProcessId($h, [ref]$pid2)
  $pn = try { (Get-Process -Id $pid2).ProcessName } catch { '?' }
  Write-Output ("HWND=0x{0:X8} PID={1} PROC={2} CLASS={3} TITLE={4}" -f $h.ToInt64(), $pid2, $pn, $cls, $title)
  return $true
}
[WinEnum]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
