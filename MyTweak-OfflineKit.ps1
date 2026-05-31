<#
.SYNOPSIS
    MyTweak offline kit for Dalamud downtime (2026-06-02).

.DESCRIPTION
    Pre-stages MyTweak into XIVLauncher's devPlugins folder and snapshots the
    currently-working Dalamud runtime so plugins keep working even if XIVLauncher
    can't fetch a fresh Dalamud during downtime.

.PARAMETER Restore
    Roll the latest snapshot back over a broken Hooks\dev\ runtime.

.PARAMETER Status
    Show what's installed, what's backed up, what's broken.

.PARAMETER Cleanup
    Remove snapshots + broken-runtime folders after Dalamud is back online.

.PARAMETER RemoveMyTweak
    With -Cleanup: also remove the MyTweak devPlugin so the user can switch back
    to pluginmaster-based installation.

.PARAMETER Force
    Skip confirmation prompts (for scripting).

.EXAMPLE
    .\MyTweak-OfflineKit.ps1
    Pre-stage everything before downtime.

.EXAMPLE
    .\MyTweak-OfflineKit.ps1 -Restore
    Restore Dalamud from snapshot if XL broke it during downtime.

.EXAMPLE
    .\MyTweak-OfflineKit.ps1 -Cleanup
    Remove snapshots after Dalamud is back. MyTweak devPlugin stays.

.EXAMPLE
    .\MyTweak-OfflineKit.ps1 -Cleanup -RemoveMyTweak -Force
    Full uninstall + remove the devPlugin without prompts.
#>

[CmdletBinding(DefaultParameterSetName='Install')]
param(
    [Parameter(ParameterSetName='Restore')][switch]$Restore,
    [Parameter(ParameterSetName='Status')][switch]$Status,
    [Parameter(ParameterSetName='Cleanup')][switch]$Cleanup,
    [Parameter(ParameterSetName='Cleanup')][switch]$RemoveMyTweak,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# ---------- Pinned constants ----------
$MyTweakVersion  = '1.0.5.3'
$MyTweakZipUrl   = "https://github.com/meowmeowmeowie1/meow/releases/download/mytweak-v$MyTweakVersion/MyTweak.zip"
$PluginMasterUrl = 'https://raw.githubusercontent.com/meowmeowmeowie1/meow/main/pluginmaster.json'

# ---------- Path resolution ----------
$XLRoot       = Join-Path $env:APPDATA 'XIVLauncher'
$HooksRoot    = Join-Path $XLRoot 'addon\Hooks'
$DalamudDev   = Join-Path $HooksRoot 'dev'
$DevPluginDir = Join-Path $XLRoot 'devPlugins\MyTweak'
$ZipCache     = Join-Path $env:TEMP 'MyTweak.zip'
$SnapshotPattern = 'dev-mytweak-snapshot-*'
$BrokenPattern   = 'dev-broken-*'

# ---------- Helpers ----------
function Write-Header($text) {
    Write-Host ''
    Write-Host '============================================================' -ForegroundColor Cyan
    Write-Host " $text" -ForegroundColor Cyan
    Write-Host '============================================================' -ForegroundColor Cyan
}

function Write-OK($text)   { Write-Host "[OK]    $text" -ForegroundColor Green }
function Write-Info($text) { Write-Host "[INFO]  $text" -ForegroundColor Gray }
function Write-Warn($text) { Write-Host "[WARN]  $text" -ForegroundColor Yellow }
function Write-Fail($text) { Write-Host "[FAIL]  $text" -ForegroundColor Red }

function Assert-XIVLauncher {
    if (-not (Test-Path $XLRoot)) {
        Write-Fail "XIVLauncher folder not found at: $XLRoot"
        Write-Fail "Install XIVLauncher and launch it once before running this kit."
        exit 1
    }
}

function Get-DalamudVersion($dir) {
    $dll = Join-Path $dir 'Dalamud.dll'
    if (-not (Test-Path $dll)) { return $null }
    try { return (Get-Item $dll).VersionInfo.FileVersion } catch { return 'unknown' }
}

function Get-MyTweakVersion {
    $manifest = Join-Path $DevPluginDir 'MyTweak.json'
    if (-not (Test-Path $manifest)) { return $null }
    try {
        # Strip BOM if present, ConvertFrom-Json in PS 5.1 chokes on it.
        $raw = (Get-Content $manifest -Raw).TrimStart([char]0xFEFF)
        return ($raw | ConvertFrom-Json).AssemblyVersion
    } catch { return 'unknown' }
}

function Get-FolderSize($path) {
    if (-not (Test-Path $path)) { return 0 }
    $sum = (Get-ChildItem $path -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    if ($null -eq $sum) { return 0 }
    return $sum
}

function Format-Bytes($bytes) {
    if ($null -eq $bytes -or $bytes -eq 0) { return '0 B' }
    $sizes = 'B','KB','MB','GB'
    $i = 0
    $n = [double]$bytes
    while ($n -ge 1024 -and $i -lt $sizes.Length - 1) { $n /= 1024; $i++ }
    return ('{0:N1} {1}' -f $n, $sizes[$i])
}

function Confirm-Action($message) {
    if ($Force) { return $true }
    Write-Host ''
    Write-Host $message -ForegroundColor Yellow
    Write-Host 'Proceed? [Y/N] ' -NoNewline -ForegroundColor Yellow
    $response = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown').Character
    Write-Host $response
    return ($response -eq 'Y' -or $response -eq 'y')
}

function Test-Url($url) {
    try {
        $r = Invoke-WebRequest -Uri $url -Method Head -UseBasicParsing -TimeoutSec 10
        return ($r.StatusCode -ge 200 -and $r.StatusCode -lt 400)
    } catch { return $false }
}

# ---------- Install mode ----------
function Invoke-Install {
    Write-Header "MyTweak Offline Kit - Install (pre-downtime)"
    Assert-XIVLauncher

    # 1. Snapshot Dalamud
    if (Test-Path $DalamudDev) {
        $dalamudVer = Get-DalamudVersion $DalamudDev
        if ($null -eq $dalamudVer) {
            Write-Warn "Hooks\dev\ exists but has no Dalamud.dll. Launch XIVLauncher once to populate it, then re-run."
        } else {
            $stamp = Get-Date -Format 'yyyy-MM-dd-HHmm'
            $snapName = "dev-mytweak-snapshot-$stamp"
            $snapPath = Join-Path $HooksRoot $snapName

            # Skip if a snapshot from same day already exists
            $todayPrefix = "dev-mytweak-snapshot-$(Get-Date -Format 'yyyy-MM-dd')-*"
            $existingToday = Get-ChildItem $HooksRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like $todayPrefix }
            if ($existingToday) {
                Write-Info "Snapshot from today already exists: $($existingToday[0].Name) - skipping snapshot step."
            } else {
                Write-Info "Snapshotting Dalamud $dalamudVer -> $snapName"
                Copy-Item $DalamudDev $snapPath -Recurse -Force
                $snapSize = Format-Bytes (Get-FolderSize $snapPath)
                Write-OK "Snapshot created ($snapSize): $snapPath"
            }
        }
    } else {
        Write-Warn "Hooks\dev\ not found. Launch XIVLauncher once, let it fetch Dalamud, then re-run."
    }

    # 2. Download MyTweak release zip
    Write-Info "Downloading MyTweak v$MyTweakVersion from GitHub Releases..."
    try {
        Invoke-WebRequest -Uri $MyTweakZipUrl -OutFile $ZipCache -UseBasicParsing -TimeoutSec 60
    } catch {
        Write-Fail "Download failed: $_"
        Write-Fail "URL: $MyTweakZipUrl"
        exit 1
    }
    $zipSize = Format-Bytes (Get-Item $ZipCache).Length
    Write-OK "Downloaded ($zipSize) to $ZipCache"

    # 3. Stage into devPlugins
    if (Test-Path $DevPluginDir) {
        Write-Info "Cleaning existing devPlugins\MyTweak\..."
        Remove-Item $DevPluginDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $DevPluginDir -Force | Out-Null
    Expand-Archive -Path $ZipCache -DestinationPath $DevPluginDir -Force

    # 4. Verify
    $dll  = Join-Path $DevPluginDir 'MyTweak.dll'
    $json = Join-Path $DevPluginDir 'MyTweak.json'
    if ((Test-Path $dll) -and (Test-Path $json)) {
        $installedVer = Get-MyTweakVersion
        Write-OK "MyTweak v$installedVer extracted to: $DevPluginDir"
    } else {
        Write-Fail "Extraction looks incomplete. Expected MyTweak.dll + MyTweak.json in $DevPluginDir"
        exit 1
    }

    # 5. Print closing tips
    Write-Host ''
    Write-Host 'Pre-staging complete. You are protected against Dalamud downtime.' -ForegroundColor Green
    Write-Host ''
    Write-Host 'Tips:' -ForegroundColor Cyan
    Write-Host '  - In XIVLauncher: Settings -> Dalamud Settings -> set "Dalamud update behavior"'
    Write-Host '    to "Manually only" so XL does not try to overwrite the runtime during downtime.'
    Write-Host '  - In Dalamud (/xlplugins): MyTweak should appear in the Dev tab.'
    Write-Host '  - If Dalamud breaks during downtime, run: .\MyTweak-OfflineKit.ps1 -Restore'
    Write-Host '  - After Dalamud is back, run:             .\MyTweak-OfflineKit.ps1 -Cleanup'
    Write-Host ''
}

# ---------- Restore mode ----------
function Invoke-Restore {
    Write-Header "MyTweak Offline Kit - Restore (during downtime)"
    Assert-XIVLauncher

    $snapshots = Get-ChildItem $HooksRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like $SnapshotPattern } | Sort-Object Name -Descending

    if (-not $snapshots) {
        Write-Fail "No snapshots found under $HooksRoot."
        Write-Fail "Did you run the kit (in install mode) before downtime?"
        exit 1
    }

    $newest = $snapshots[0]
    Write-Info "Newest snapshot: $($newest.Name)"

    # Move any current broken dev\ aside
    if (Test-Path $DalamudDev) {
        $currentVer = Get-DalamudVersion $DalamudDev
        $snapshotVer = Get-DalamudVersion $newest.FullName
        if ($currentVer -eq $snapshotVer -and $null -ne $currentVer) {
            Write-Info "Hooks\dev\ already matches the snapshot (Dalamud $currentVer). Nothing to restore."
            return
        }
        $stamp = Get-Date -Format 'yyyy-MM-dd-HHmm'
        $brokenName = "dev-broken-$stamp"
        $brokenPath = Join-Path $HooksRoot $brokenName
        $verDisplay = if ($currentVer) { $currentVer } else { 'missing' }
        Write-Info "Setting current dev\ aside as $brokenName (current Dalamud: $verDisplay)"
        Move-Item $DalamudDev $brokenPath
    }

    Write-Info "Restoring snapshot..."
    Copy-Item $newest.FullName $DalamudDev -Recurse -Force
    $restoredVer = Get-DalamudVersion $DalamudDev
    if ($null -eq $restoredVer) {
        Write-Fail "Restore finished but Dalamud.dll is missing in the new dev\. Manual intervention needed."
        exit 1
    }
    Write-OK "Restored Dalamud $restoredVer to: $DalamudDev"
    Write-Host ''
    Write-Host 'Now relaunch FFXIV via XIVLauncher. Dalamud + MyTweak should load normally.' -ForegroundColor Green
    Write-Host ''
}

# ---------- Status mode ----------
function Invoke-Status {
    Write-Header "MyTweak Offline Kit - Status"
    Assert-XIVLauncher

    $dalamudVer = Get-DalamudVersion $DalamudDev
    $snapshots = @(Get-ChildItem $HooksRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like $SnapshotPattern })
    $broken    = @(Get-ChildItem $HooksRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like $BrokenPattern })
    $mtVer     = Get-MyTweakVersion

    $lines = @()
    $lines += [PSCustomObject]@{ Item = 'Dalamud runtime (Hooks\dev\)'; State = if ($dalamudVer) { "OK   (Dalamud.dll $dalamudVer)" } else { 'MISSING' } }
    $lines += [PSCustomObject]@{ Item = 'Snapshots available';          State = if ($snapshots.Count -gt 0) { "$($snapshots.Count) (newest: $(($snapshots | Sort-Object Name -Descending)[0].Name))" } else { '0' } }
    $lines += [PSCustomObject]@{ Item = 'Broken runtimes set aside';    State = "$($broken.Count)" }
    $lines += [PSCustomObject]@{ Item = 'MyTweak devPlugin';            State = if ($mtVer) { "OK   (v$mtVer)" } else { 'NOT INSTALLED' } }

    Write-Host ''
    $lines | Format-Table -AutoSize
    Write-Host ''
}

# ---------- Cleanup mode ----------
function Invoke-Cleanup {
    Write-Header "MyTweak Offline Kit - Cleanup (post-downtime)"
    Assert-XIVLauncher

    # Safety: only proceed if live Hooks\dev\ has a valid Dalamud.dll
    $liveVer = Get-DalamudVersion $DalamudDev
    if ($null -eq $liveVer) {
        Write-Fail "Hooks\dev\ has no working Dalamud.dll right now. Refusing to delete snapshots."
        Write-Fail "Either restore first (-Restore) or launch XIVLauncher so it can refresh Dalamud."
        exit 1
    }
    Write-Info "Live Dalamud runtime is OK (v$liveVer). Safe to clean up."

    $snapshots = @(Get-ChildItem $HooksRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like $SnapshotPattern })
    $broken    = @(Get-ChildItem $HooksRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like $BrokenPattern })

    $targets = @()
    $targets += $snapshots
    $targets += $broken
    if (Test-Path $ZipCache) { $targets += (Get-Item $ZipCache) }

    if ($RemoveMyTweak -and (Test-Path $DevPluginDir)) {
        # Sanity-check pluginmaster is reachable so we do not strand the user
        if (Test-Url $PluginMasterUrl) {
            Write-Info "Pluginmaster reachable; safe to remove the devPlugin."
            $targets += (Get-Item $DevPluginDir)
        } else {
            Write-Warn "Pluginmaster URL not reachable. Refusing to remove devPlugins\MyTweak\ - you might lose the only copy."
            Write-Warn "Re-run later with -Cleanup -RemoveMyTweak when network is back, or omit -RemoveMyTweak."
        }
    }

    if (-not $targets) {
        Write-OK "Nothing to clean up. System is already in a clean state."
        return
    }

    Write-Host ''
    Write-Host "About to delete the following:" -ForegroundColor Yellow
    $totalSize = 0L
    foreach ($t in $targets) {
        $sz = if ($t.PSIsContainer) { Get-FolderSize $t.FullName } else { $t.Length }
        $totalSize += $sz
        Write-Host ("  {0,-12} {1}" -f (Format-Bytes $sz), $t.FullName)
    }
    Write-Host ("  Total: {0}" -f (Format-Bytes $totalSize)) -ForegroundColor Yellow

    if (-not (Confirm-Action 'Confirm deletion?')) {
        Write-Info "Cancelled."
        return
    }

    $deleted = 0
    foreach ($t in $targets) {
        try {
            Remove-Item $t.FullName -Recurse -Force
            $deleted++
        } catch {
            Write-Warn "Could not delete $($t.FullName): $_"
        }
    }
    Write-OK "Cleanup complete. Removed $deleted item(s), freed $(Format-Bytes $totalSize)."
    Write-Host ''
    if (-not $RemoveMyTweak) {
        Write-Info "MyTweak devPlugin preserved at: $DevPluginDir"
        Write-Info "It will keep loading as a dev plugin. To switch to pluginmaster-based install,"
        Write-Info "re-run with: -Cleanup -RemoveMyTweak"
    }
}

# ---------- Dispatch ----------
switch ($PSCmdlet.ParameterSetName) {
    'Restore' { Invoke-Restore }
    'Status'  { Invoke-Status }
    'Cleanup' { Invoke-Cleanup }
    default   { Invoke-Install }
}
