$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\GrailFloatingText.cs") -Raw
$methodStart = $source.IndexOf("private void ShowVanillaWyrdNotification(", [StringComparison]::Ordinal)
$nextMethod = $source.IndexOf("private void ShowDefaultGameNotification(", $methodStart, [StringComparison]::Ordinal)
if ($methodStart -lt 0 -or $nextMethod -le $methodStart) {
    throw "Could not locate the vanilla Wyrd notification method."
}

$method = $source.Substring($methodStart, $nextMethod - $methodStart)
$readiness = $method.IndexOf("IsGameLoadedReadyForNotifications()", [StringComparison]::Ordinal)
$throttle = $method.IndexOf("ShouldThrottleDefaultGameEvent", [StringComparison]::Ordinal)
if ($readiness -lt 0 -or $throttle -lt 0 -or $readiness -ge $throttle) {
    throw "Vanilla Wyrd notifications must check loaded-game readiness before consuming their throttle."
}

if ($method.Contains("QueueDeferredNotification")) {
    throw "Vanilla Wyrd state changes must be discarded during loading, not deferred."
}

if (-not $method.Contains('ResolveEyesWyrdStyle()') -or
    -not $source.Contains('return "Orange";') -or
    -not $source.Contains('return "Purple";') -or
    $method.Contains('"Wyrd"')) {
    throw "Vanilla Wyrd notifications must request the Eyes-selected Purple or Orange color group."
}

if (-not $source.Contains('description + " Default: " + defaultColor')) {
    throw "Color-group descriptions must show each setting's actual default color."
}

Write-Host "Grail Floating Text Wyrd routing and color config contract passed."
