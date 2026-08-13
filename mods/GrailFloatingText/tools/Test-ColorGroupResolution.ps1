[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $modRoot "src\GrailFloatingText.cs"
$source = Get-Content -LiteralPath $sourcePath -Raw

$methodStart = $source.IndexOf("private Color ResolveStyleColor(", [StringComparison]::Ordinal)
$methodEnd = $source.IndexOf("private Color ResolveNamedGroupOrFallback(", $methodStart, [StringComparison]::Ordinal)
if ($methodStart -lt 0 -or $methodEnd -le $methodStart) {
    throw "Could not locate the ResolveStyleColor method body."
}

$method = $source.Substring($methodStart, $methodEnd - $methodStart)
$groupLookup = $method.IndexOf("_colorGroupByName.TryGetValue", [StringComparison]::Ordinal)
$htmlParse = $method.IndexOf("ColorUtility.TryParseHtmlString", [StringComparison]::Ordinal)
if ($groupLookup -lt 0) {
    throw "ResolveStyleColor no longer checks configured color groups."
}
if ($htmlParse -lt 0) {
    throw "ResolveStyleColor no longer supports literal HTML colors."
}
if ($groupLookup -gt $htmlParse) {
    throw "Literal HTML colors are resolved before configured color groups. Named colors such as Purple would bypass their settings."
}

$requiredGroups = @("Red", "Gold", "Blue", "Green", "Purple", "Pink", "Orange", "Pale", "Gray", "White", "Default")
$bindingsStart = $source.IndexOf("private void BindColorGroups()", [StringComparison]::Ordinal)
$bindingsEnd = $source.IndexOf("private void BindColorGroup(", $bindingsStart, [StringComparison]::Ordinal)
if ($bindingsStart -lt 0 -or $bindingsEnd -le $bindingsStart) {
    throw "Could not locate the BindColorGroups method body."
}

$bindings = $source.Substring($bindingsStart, $bindingsEnd - $bindingsStart)
foreach ($groupName in $requiredGroups) {
    if ($bindings.IndexOf(('"{0}"' -f $groupName), [StringComparison]::Ordinal) -lt 0) {
        throw "Missing configured color group binding: $groupName"
    }
}

if (-not [regex]::IsMatch($bindings, '"Purple"\s*,\s*"#C294FF"')) {
    throw "Purple must bind with the authored #C294FF default."
}
if (-not [regex]::IsMatch($bindings, '"Pink"\s*,\s*"#E06AAE"')) {
    throw "Pink must bind with the authored #E06AAE default."
}
if (-not [regex]::IsMatch($bindings, '"Orange"\s*,\s*"#FF9A35"')) {
    throw "Orange must bind with the authored #FF9A35 default."
}
if (-not [regex]::IsMatch($bindings, '"Gold"\s*,\s*"#FFC03A"')) {
    throw "Gold must bind with the authored #FFC03A default."
}

Write-Output "Color-group resolution contract passed."
