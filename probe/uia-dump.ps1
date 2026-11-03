# Dump UIA tree of the ChatGPT/Codex desktop window (Electron) - UTF-8 output
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$out = New-Object System.Collections.Generic.List[string]

function Dump-Node($el, $depth, $maxDepth) {
    if ($null -eq $el -or $depth -gt $maxDepth) { return }
    $ct = $el.Current.ControlType.ProgrammaticName -replace 'ControlType\.',''
    $nm = $el.Current.Name
    $aid = $el.Current.AutomationId
    $cls = $el.Current.ClassName
    $fw = $el.Current.FrameworkId
    $pid2 = $el.Current.ProcessId
    $r = $el.Current.BoundingRectangle
    $rect = "{0},{1},{2}x{3}" -f [int]$r.X, [int]$r.Y, [int]$r.Width, [int]$r.Height
    $pat = @()
    if ($el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern) -ne $null) { $pat += 'Value' }
    if ($el.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern) -ne $null) { $pat += 'Text' }
    if ($el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) -ne $null) { $pat += 'Invoke' }
    if ($el.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern) -ne $null) { $pat += 'Range' }
    $line = ("  " * $depth) + "[$ct] name='$nm' id='$aid' class='$cls' fw='$fw' rect=$rect pat=$($pat -join ',')"
    $out.Add($line)

    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $child = $walker.GetFirstChild($el)
    while ($null -ne $child) {
        Dump-Node $child ($depth + 1) $maxDepth
        $child = $walker.GetNextSibling($child)
    }
}

# find windows by process name
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, 17660)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
$out.Add("WINDOWS_FOUND=$($wins.Count)")
foreach ($w in $wins) {
    $out.Add("=== WINDOW name='$($w.Current.Name)' class='$($w.Current.ClassName)' rect=$($w.Current.BoundingRectangle)")
    Dump-Node $w 0 28
}

# also try the terminal-hosted codex tab (pid 6012) just for title context
$cond2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, 6012)
$wins2 = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond2)
foreach ($w in $wins2) {
    $out.Add("=== TERM WINDOW name='$($w.Current.Name)'")
}

[System.IO.File]::WriteAllLines('D:\Desktop\Codex-Continue\probe\uia-tree.txt', $out, (New-Object System.Text.UTF8Encoding($false)))
Write-Output ("WROTE " + $out.Count + " lines")
