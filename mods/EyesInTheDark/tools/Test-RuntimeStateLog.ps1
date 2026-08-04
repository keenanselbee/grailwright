param(
    [string]$LogPath = "G:\Steam\steamapps\common\Tainted Grail FoA\BepInEx\LogOutput.log",
    [switch]$ParserOnly
)

$ErrorActionPreference = "Stop"

$statePattern = "Night state: (?<state>[^;]+); reason=(?<reason>[^;]+)(?:; huntState=(?<huntState>[^;]+))?; scene=(?<scene>[^;]+); paused=(?<paused>true|false); protection=(?<protection>[^;]+); nightProgress=(?<progress>[0-9.]+); activeRealSeconds=(?<seconds>[0-9.]+)"

foreach ($sample in @(
    "Night state: Roaming; reason=None; scene=CampaignMap_HOS; paused=false; protection=exposed; nightProgress=0.2; activeRealSeconds=10.0",
    "Night state: Roaming; reason=None; huntState=ActiveHunt; scene=CampaignMap_HOS; paused=false; protection=exposed; nightProgress=0.2; activeRealSeconds=10.0"
)) {
    if ($sample -notmatch $statePattern) {
        throw "Runtime-state parser rejected a supported log format: $sample"
    }
}

if ($ParserOnly) {
    Write-Host "Eyes in the Dark old/current runtime-state log parser contracts passed."
    return
}

if (!(Test-Path -LiteralPath $LogPath)) {
    throw "BepInEx log not found: $LogPath"
}

$modRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$manifest = Get-Content -LiteralPath (Join-Path $modRoot "mod.json") -Raw | ConvertFrom-Json
$pluginSource = Get-Content -LiteralPath (Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
$pluginNameMatch = [regex]::Match(
    $pluginSource,
    'PluginName\s*=\s*"(?<name>[^"]+)"'
)
if (!$pluginNameMatch.Success) {
    throw "Could not read PluginName from EyesInTheDark.cs."
}
$pluginName = $pluginNameMatch.Groups['name'].Value
$lines = @(Get-Content -LiteralPath $LogPath)
$pluginLines = @($lines | Where-Object { $_ -match "Eyes in the Dark" })
$states = @()

for ($index = 0; $index -lt $lines.Count; $index++) {
    if ($lines[$index] -match $statePattern) {
        $states += [pscustomobject]@{
            Index = $index
            State = $Matches.state
            Reason = $Matches.reason
            HuntState = $Matches.huntState
            Scene = $Matches.scene
            Paused = $Matches.paused -eq "true"
            Protection = $Matches.protection
            Progress = [double]::Parse($Matches.progress, [Globalization.CultureInfo]::InvariantCulture)
            ActiveSeconds = [double]::Parse($Matches.seconds, [Globalization.CultureInfo]::InvariantCulture)
        }
    }
}

$results = [System.Collections.Generic.List[object]]::new()
function Add-Result([string]$Check, [bool]$Passed, [string]$Evidence) {
    $results.Add([pscustomobject]@{
        Check = $Check
        Result = if ($Passed) { "PASS" } else { "MISSING" }
        Evidence = $Evidence
    })
}

$loaded = @($pluginLines | Where-Object { $_ -match "Loading \[$([regex]::Escape($pluginName)) $([regex]::Escape($manifest.version))\]" })
$pluginFailures = @($pluginLines | Where-Object { $_ -match "^\[(Warning|Error|Fatal)" -or $_ -match "failed closed|Exception" })
Add-Result "Expected plugin version loaded" ($loaded.Count -gt 0) "$($loaded.Count) matching load entry or entries"
Add-Result "No Eyes startup/runtime failure" ($pluginFailures.Count -eq 0) "$($pluginFailures.Count) warning/error/exception entry or entries"
Add-Result "Main menu or missing hero inactive" (@($states | Where-Object Reason -eq "NoPlayableHero").Count -gt 0) "reason=NoPlayableHero"
Add-Result "Loading suppressed" (@($states | Where-Object Reason -eq "Loading").Count -gt 0) "reason=Loading"
Add-Result "Scene transition suppressed" (@($states | Where-Object Reason -eq "Transition").Count -gt 0) "reason=Transition"
Add-Result "Outdoor daylight inactive" (@($states | Where-Object Reason -eq "Daylight").Count -gt 0) "reason=Daylight"
Add-Result "Outdoor Wyrd Night roaming" (@($states | Where-Object { $_.State -eq "Roaming" -and $_.Reason -eq "None" }).Count -gt 0) "state=Roaming"
Add-Result "Protected outdoor Wyrd Night" (@($states | Where-Object { $_.State -eq "Roaming" -and $_.Protection -eq "protected" }).Count -gt 0) "Roaming protection=protected"
Add-Result "Exposed outdoor Wyrd Night" (@($states | Where-Object { $_.State -eq "Roaming" -and $_.Protection -eq "exposed" }).Count -gt 0) "Roaming protection=exposed"
Add-Result "Interior inactive" (@($states | Where-Object Reason -eq "NotOutdoor").Count -gt 0) "reason=NotOutdoor"
Add-Result "Portal or fast travel suppressed" (@($states | Where-Object Reason -eq "Travel").Count -gt 0) "reason=Travel"
Add-Result "Rest suppressed" (@($states | Where-Object Reason -eq "Resting").Count -gt 0) "reason=Resting"
Add-Result "Death suppressed" (@($states | Where-Object Reason -eq "HeroDead").Count -gt 0) "reason=HeroDead"

$pauseProof = $null
foreach ($pausedState in @($states | Where-Object { $_.Paused -and $_.Scene -ne "<unknown>" })) {
    $resumedState = $states |
        Where-Object {
            ($_.Index -gt $pausedState.Index -and
                !$_.Paused -and
                $_.Scene -eq $pausedState.Scene)
        } |
        Select-Object -First 1
    if ($null -ne $resumedState) {
        $delta = $resumedState.ActiveSeconds - $pausedState.ActiveSeconds
        if ($delta -ge 0.0 -and $delta -le 0.6) {
            $pauseProof = "activeRealSeconds delta=$($delta.ToString('0.0', [Globalization.CultureInfo]::InvariantCulture))"
            break
        }
    }
}
Add-Result "Pause stops active-real-time clock" ($null -ne $pauseProof) $(if ($pauseProof) { $pauseProof } else { "pause followed by same-scene unpause not found" })

$postGameplayLoad = $false
$firstRoaming = $states | Where-Object State -eq "Roaming" | Select-Object -First 1
if ($null -ne $firstRoaming) {
    $postGameplayLoad = @($states | Where-Object { $_.Index -gt $firstRoaming.Index -and $_.Reason -eq "Loading" }).Count -gt 0
}
Add-Result "Reload after gameplay suppressed" $postGameplayLoad "Loading entry after first Roaming entry"

$completedCycle = $false
foreach ($roaming in @($states | Where-Object State -eq "Roaming")) {
    if (@($states | Where-Object { $_.Index -gt $roaming.Index -and $_.Reason -eq "Daylight" }).Count -gt 0) {
        $completedCycle = $true
        break
    }
}
Add-Result "Dawn returns director inactive" $completedCycle "Roaming followed by reason=Daylight"

$results | Format-Table -AutoSize
$missing = @($results | Where-Object Result -ne "PASS")
if ($missing.Count -gt 0) {
    Write-Host "Runtime state gate incomplete: $($missing.Count) check(s) still missing."
    exit 1
}

Write-Host "Eyes in the Dark runtime state gate passed."
