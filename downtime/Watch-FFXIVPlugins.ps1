<#
.SYNOPSIS
    Patch-day watcher: tells you the moment Dalamud and FFXIV_ACT_Plugin are
    updated for the current game version.

.DESCRIPTION
    After every FFXIV patch, Dalamud and ravahn's FFXIV_ACT_Plugin are broken
    until their authors ship updates for the new game version. Nothing local
    can fix that (memory offsets and network opcodes change), so the best
    tool is one that watches upstream and pings you the second things are back.

    This script:
      - Reads your installed game version from ffxivgame.ver
      - Asks the official Dalamud release API which game version it supports
      - Checks ravahn/FFXIV_ACT_Plugin's latest GitHub release date against
        the patch date encoded in your game version
      - In -Watch mode, polls on an interval and fires a Windows toast +
        beeps when a component flips to READY, exiting once everything is up.

.PARAMETER Watch
    Keep polling until both Dalamud and FFXIV_ACT_Plugin are ready.

.PARAMETER IntervalMinutes
    Poll interval for -Watch mode. Default 10.

.PARAMETER GamePath
    Override the FFXIV install root (the folder that contains 'game\').
    Auto-detected from XIVLauncher config or default install paths otherwise.

.EXAMPLE
    .\Watch-FFXIVPlugins.ps1
    One-shot status check.

.EXAMPLE
    .\Watch-FFXIVPlugins.ps1 -Watch
    Poll every 10 minutes, toast + beep when plugins come back, then exit.
#>

[CmdletBinding()]
param(
    [switch]$Watch,
    [ValidateRange(1, 120)][int]$IntervalMinutes = 10,
    [string]$GamePath
)

$ErrorActionPreference = 'Stop'

$DalamudVersionUrl = 'https://kamori.goats.dev/Dalamud/Release/VersionInfo?track=release'
$ActReleaseUrl     = 'https://api.github.com/repos/ravahn/FFXIV_ACT_Plugin/releases/latest'

function Write-OK($text)   { Write-Host "[READY]   $text" -ForegroundColor Green }
function Write-Wait($text) { Write-Host "[PENDING] $text" -ForegroundColor Yellow }
function Write-Info($text) { Write-Host "[INFO]    $text" -ForegroundColor Gray }
function Write-Fail($text) { Write-Host "[ERROR]   $text" -ForegroundColor Red }

function Show-Toast($title, $message) {
    try {
        [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
        $template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
        $texts = $template.GetElementsByTagName('text')
        $texts.Item(0).AppendChild($template.CreateTextNode($title)) | Out-Null
        $texts.Item(1).AppendChild($template.CreateTextNode($message)) | Out-Null
        $toast = [Windows.UI.Notifications.ToastNotification]::new($template)
        [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('FFXIV Plugin Watch').Show($toast)
    } catch {
        # Toast API unavailable (e.g. PS session without WinRT) - beep instead.
    }
    try {
        [console]::beep(880, 250); [console]::beep(1100, 250); [console]::beep(1320, 400)
    } catch { }
}

function Get-GameRoot {
    if ($GamePath) {
        if (Test-Path (Join-Path $GamePath 'game\ffxivgame.ver')) { return $GamePath }
        Write-Fail "No game\ffxivgame.ver under: $GamePath"
        exit 1
    }

    # XIVLauncher knows the real install path.
    $xlConfig = Join-Path $env:APPDATA 'XIVLauncher\launcherConfigV3.json'
    if (Test-Path $xlConfig) {
        try {
            $raw = (Get-Content $xlConfig -Raw).TrimStart([char]0xFEFF)
            $configured = ($raw | ConvertFrom-Json).GamePath
            if ($configured -and (Test-Path (Join-Path $configured 'game\ffxivgame.ver'))) { return $configured }
        } catch { }
    }

    $candidates = @(
        "${env:ProgramFiles(x86)}\SQUARE ENIX\FINAL FANTASY XIV - A Realm Reborn",
        "$env:ProgramFiles\SQUARE ENIX\FINAL FANTASY XIV - A Realm Reborn"
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path (Join-Path $c 'game\ffxivgame.ver'))) { return $c }
    }
    return $null
}

function Get-GameVersion {
    $root = Get-GameRoot
    if (-not $root) { return $null }
    (Get-Content (Join-Path $root 'game\ffxivgame.ver') -Raw).Trim()
}

function Get-PatchDate($gameVer) {
    # Game versions look like 2026.07.16.0000.0000 - the leading segments are
    # the build date of the patch.
    if ($gameVer -match '^(\d{4})\.(\d{2})\.(\d{2})') {
        return [datetime]::new([int]$Matches[1], [int]$Matches[2], [int]$Matches[3], 0, 0, 0, [DateTimeKind]::Utc)
    }
    return $null
}

function Get-DalamudStatus($gameVer) {
    try {
        $info = Invoke-RestMethod -Uri $DalamudVersionUrl -UseBasicParsing -TimeoutSec 20
    } catch {
        return [PSCustomObject]@{ Name = 'Dalamud'; State = 'UNKNOWN'; Detail = "API unreachable: $($_.Exception.Message)" }
    }
    $supported = $info.supportedGameVer
    $assembly  = $info.assemblyVersion
    if (-not $gameVer) {
        return [PSCustomObject]@{ Name = 'Dalamud'; State = 'UNKNOWN'; Detail = "v$assembly supports game $supported (local game version not found - compare manually)" }
    }
    if ($supported -eq $gameVer) {
        return [PSCustomObject]@{ Name = 'Dalamud'; State = 'READY'; Detail = "v$assembly supports your game version ($gameVer)" }
    }
    return [PSCustomObject]@{ Name = 'Dalamud'; State = 'PENDING'; Detail = "v$assembly supports $supported, you are on $gameVer" }
}

function Get-ActStatus($patchDate) {
    try {
        $rel = Invoke-RestMethod -Uri $ActReleaseUrl -UseBasicParsing -TimeoutSec 20 -Headers @{ 'User-Agent' = 'ffxiv-plugin-watch' }
    } catch {
        return [PSCustomObject]@{ Name = 'FFXIV_ACT_Plugin'; State = 'UNKNOWN'; Detail = "GitHub API unreachable: $($_.Exception.Message)" }
    }
    $published = [datetime]$rel.published_at
    $tag = $rel.tag_name
    if (-not $patchDate) {
        return [PSCustomObject]@{ Name = 'FFXIV_ACT_Plugin'; State = 'UNKNOWN'; Detail = "latest release $tag published $($published.ToString('yyyy-MM-dd')) (patch date unknown - compare manually)" }
    }
    if ($published -ge $patchDate) {
        return [PSCustomObject]@{ Name = 'FFXIV_ACT_Plugin'; State = 'READY'; Detail = "$tag published $($published.ToString('yyyy-MM-dd')), after patch day $($patchDate.ToString('yyyy-MM-dd'))" }
    }
    return [PSCustomObject]@{ Name = 'FFXIV_ACT_Plugin'; State = 'PENDING'; Detail = "$tag published $($published.ToString('yyyy-MM-dd')), before patch day $($patchDate.ToString('yyyy-MM-dd'))" }
}

function Write-Status($statuses, $gameVer) {
    Write-Host ''
    Write-Host '=============================================' -ForegroundColor Cyan
    Write-Host " FFXIV plugin status  $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -ForegroundColor Cyan
    Write-Host '=============================================' -ForegroundColor Cyan
    if ($gameVer) { Write-Info "Installed game version: $gameVer" }
    else          { Write-Info 'Installed game version: not detected (use -GamePath to point at your install)' }
    foreach ($s in $statuses) {
        switch ($s.State) {
            'READY'   { Write-OK   "$($s.Name): $($s.Detail)" }
            'PENDING' { Write-Wait "$($s.Name): $($s.Detail)" }
            default   { Write-Fail "$($s.Name): $($s.Detail)" }
        }
    }
    Write-Host ''
}

# ---------- Main ----------
$gameVer   = Get-GameVersion
$patchDate = Get-PatchDate $gameVer

$previous = @{}
while ($true) {
    $statuses = @(
        (Get-DalamudStatus $gameVer),
        (Get-ActStatus $patchDate)
    )
    Write-Status $statuses $gameVer

    foreach ($s in $statuses) {
        if ($s.State -eq 'READY' -and $previous[$s.Name] -and $previous[$s.Name] -ne 'READY') {
            Show-Toast "$($s.Name) is back!" $s.Detail
        }
        $previous[$s.Name] = $s.State
    }

    $allReady = -not ($statuses | Where-Object { $_.State -ne 'READY' })
    if (-not $Watch) { break }
    if ($allReady) {
        Show-Toast 'All plugins ready' 'Dalamud and FFXIV_ACT_Plugin both support the current patch.'
        Write-OK 'Everything is updated - stopping watch.'
        break
    }
    Write-Info "Next check in $IntervalMinutes minute(s). Ctrl+C to stop."
    Start-Sleep -Seconds ($IntervalMinutes * 60)
}
