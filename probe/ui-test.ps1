# Behavior test: theme switch + maximize/restore via UIA
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W {
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
}
'@

$out = New-Object System.Collections.Generic.List[string]
$proc = Get-Process CodexContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { $out.Add("NO_APP"); [IO.File]::WriteAllLines('D:\Desktop\Codex-Continue\probe\uitest-out.txt', $out); exit }
$hwnd = $proc.MainWindowHandle
$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)

function Find-ByName($root, $name, $type) {
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)),
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type)))
    $found = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    return $found
}

# 1. theme dark
$dark = Find-ByName $root "深色" ([System.Windows.Automation.ControlType]::RadioButton)
if ($dark) {
    $r = $dark.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $r.Select()
    Start-Sleep -Milliseconds 600
    $out.Add("THEME_DARK selected")
} else { $out.Add("THEME_DARK not found") }

# 2. back to light
$light = Find-ByName $root "浅色" ([System.Windows.Automation.ControlType]::RadioButton)
if ($light) {
    $r = $light.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $r.Select()
    Start-Sleep -Milliseconds 600
    $out.Add("THEME_LIGHT selected")
}

# 3. arm button state (label)
$arm = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, "BtnArm")))
if ($arm) { $out.Add("BTN_ARM found name='$($arm.Current.Name)' enabled=$($arm.Current.IsEnabled)") }

# 4. maximize via style (native) — click max caption button
$max = Find-ByName $root "" ([System.Windows.Automation.ControlType]::Button) # fallback below
# caption buttons have empty names; use the 2nd 69x60 rect button
$btns = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
$caps = @()
foreach ($b in $btns) {
    $r = $b.Current.BoundingRectangle
    if ($r.Height -ge 50 -and $r.Width -ge 60 -and $r.Y -lt 250) { $caps += $b }
}
$out.Add("CAPTION_BTNS=$($caps.Count)")
if ($caps.Count -ge 3) {
    # order: min, max, close (left to right)
    $caps = $caps | Sort-Object { $_.Current.BoundingRectangle.X }
    $caps[1].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 900
    $out.Add("AFTER_MAX IsZoomed=$([W]::IsZoomed($hwnd))")
    $caps[1].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 900
    $out.Add("AFTER_RESTORE IsZoomed=$([W]::IsZoomed($hwnd))")
}

# 5. drag-test marker: window still alive
$proc2 = Get-Process CodexContinue -ErrorAction SilentlyContinue
$out.Add("ALIVE=$([bool]$proc2)")

[IO.File]::WriteAllLines('D:\Desktop\Codex-Continue\probe\uitest-out.txt', $out, (New-Object System.Text.UTF8Encoding($false)))
Write-Output "WROTE $($out.Count)"
