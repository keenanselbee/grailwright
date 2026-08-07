[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (
    Join-Path $modRoot "src\GrailFloatingText.cs") -Raw

function Assert-DeferredPersistenceContract {
    param([bool]$Condition, [string]$Message)
    if (!$Condition) {
        throw "Grail Floating Text deferred persistence contract failed: $Message"
    }
}

$start = $source.IndexOf('private void SaveDeferredNotifications()')
$end = $source.IndexOf(
    'private string GetDeferredNotificationStorePath()',
    $start)
Assert-DeferredPersistenceContract (
    $start -ge 0 -and $end -gt $start) (
    "save method could not be isolated")

$body = $source.Substring($start, $end - $start)
foreach ($required in @(
    'File.WriteAllLines(temporaryPath, lines, Encoding.UTF8);',
    'File.Replace(temporaryPath, path, null);',
    'File.Move(temporaryPath, path);')) {
    Assert-DeferredPersistenceContract ($body.Contains($required)) "save method omits $required"
}

$replaceIndex = $body.IndexOf('File.Replace(temporaryPath, path, null);')
$deleteIndex = $body.IndexOf('File.Delete(path);', $body.IndexOf('string temporaryPath'))
Assert-DeferredPersistenceContract ($replaceIndex -ge 0 -and $deleteIndex -lt 0) (
    "active deferred-notification file is deleted before replacement")

Write-Host "Grail Floating Text deferred persistence contracts passed."
