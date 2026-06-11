<#
================================================================================
  FFXIV ReShade - Clean Reinstall Helper
  Target setup: ReShade (add-on) + Alive NEXT + version-matched iMMERSE / METEOR
                + your Patreon iMMERSE Pro on top
--------------------------------------------------------------------------------
  What this does FOR you (automatable):
    1. Finds your FFXIV \game\ folder (registry + common paths, or you point it).
    2. Backs up EVERYTHING reshade-related to a timestamped folder.
    3. Fully DELETES the shader graveyard (reshade-shaders / reshade-presets)
       so nothing old gets merged back in.
    4. Optionally pulls FREE iMMERSE + METEOR from Marty's GitHub (latest release)
       so versions match instead of inheriting GPosingway's pin.
    5. Sets RESHADE_DEPTH_INPUT_IS_REVERSED = 1 in ReShade.ini (Dawntrail fix).

  What it CANNOT do (manual - the script tells you exactly when):
    - Run the ReShade installer GUI (pick d3d11/DXGI + Add-on support).
    - Download paid iMMERSE Pro from your Patreon.
    - Download Alive NEXT from Nexus (login-walled).

  USAGE (PowerShell, in the folder with this file):
      ./ffxiv-reshade-clean-reinstall.ps1                 # interactive, safe
      ./ffxiv-reshade-clean-reinstall.ps1 -GamePath "D:\FFXIV\game"
      ./ffxiv-reshade-clean-reinstall.ps1 -SkipMartyDownload   # back up + wipe only
      ./ffxiv-reshade-clean-reinstall.ps1 -WhatIf          # dry run, deletes nothing

  If you get an execution-policy error, run this once in the same window:
      Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
================================================================================
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    # Path to your FFXIV \game\ folder (the one containing ffxiv_dx11.exe).
    [string] $GamePath,

    # Where to put the backup. Defaults to Desktop\FFXIV_ReShade_Backup_<timestamp>.
    [string] $BackupPath,

    # Skip pulling free iMMERSE/METEOR from GitHub (just back up + wipe clean).
    [switch] $SkipMartyDownload
)

$ErrorActionPreference = 'Stop'

function Write-Step  { param($m) Write-Host "`n==> $m" -ForegroundColor Cyan }
function Write-Ok    { param($m) Write-Host "    [ok]   $m" -ForegroundColor Green }
function Write-Warn2 { param($m) Write-Host "    [warn] $m" -ForegroundColor Yellow }
function Write-Todo  { param($m) Write-Host "    [YOU]  $m" -ForegroundColor Magenta }

# -----------------------------------------------------------------------------
# 1. Locate the FFXIV \game\ folder
# -----------------------------------------------------------------------------
function Find-GameFolder {
    param([string] $Explicit)

    if ($Explicit) {
        if (Test-Path (Join-Path $Explicit 'ffxiv_dx11.exe')) { return (Resolve-Path $Explicit).Path }
        throw "ffxiv_dx11.exe not found in '$Explicit'. Point -GamePath at the \game\ folder."
    }

    $candidates = New-Object System.Collections.Generic.List[string]

    # Registry: official launcher install path
    $regKeys = @(
        'HKLM:\SOFTWARE\WOW6432Node\SquareEnix\FINAL FANTASY XIV - A Realm Reborn',
        'HKLM:\SOFTWARE\SquareEnix\FINAL FANTASY XIV - A Realm Reborn'
    )
    foreach ($k in $regKeys) {
        try {
            $p = (Get-ItemProperty -Path $k -ErrorAction Stop).InstallLocation
            if ($p) { $candidates.Add((Join-Path $p 'game')) }
        } catch { }
    }

    # Common manual install locations + Steam libraries
    $common = @(
        "$env:ProgramFiles\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game",
        "${env:ProgramFiles(x86)}\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game",
        "${env:ProgramFiles(x86)}\Steam\steamapps\common\FINAL FANTASY XIV Online\game",
        "C:\Program Files (x86)\FINAL FANTASY XIV - A Realm Reborn\game"
    )
    $common | ForEach-Object { $candidates.Add($_) }

    # XIVLauncher config sometimes records the path
    $xl = Join-Path $env:APPDATA 'XIVLauncher\launcherConfigV3.json'
    if (Test-Path $xl) {
        try {
            $cfg = Get-Content $xl -Raw | ConvertFrom-Json
            if ($cfg.GamePath) { $candidates.Add((Join-Path $cfg.GamePath 'game')) }
        } catch { }
    }

    foreach ($c in $candidates) {
        if ($c -and (Test-Path (Join-Path $c 'ffxiv_dx11.exe'))) { return (Resolve-Path $c).Path }
    }
    return $null
}

Write-Step "Locating FFXIV game folder"
$game = Find-GameFolder -Explicit $GamePath
if (-not $game) {
    Write-Warn2 "Could not auto-detect FFXIV. Re-run with:  -GamePath `"X:\path\to\FFXIV\game`""
    $game = Read-Host "    Or paste the full path to your \game\ folder now"
    if (-not (Test-Path (Join-Path $game 'ffxiv_dx11.exe'))) { throw "ffxiv_dx11.exe not found there. Aborting." }
    $game = (Resolve-Path $game).Path
}
Write-Ok "Game folder: $game"

# Make sure the game isn't running (file locks)
if (Get-Process -Name 'ffxiv_dx11','ffxiv' -ErrorAction SilentlyContinue) {
    throw "FFXIV is running. Close the game (and launcher) and run this again."
}

# -----------------------------------------------------------------------------
# 2. Back up everything ReShade-related
# -----------------------------------------------------------------------------
$stamp = Get-Date -Format 'yyyy-MM-dd_HHmmss'
if (-not $BackupPath) {
    $BackupPath = Join-Path ([Environment]::GetFolderPath('Desktop')) "FFXIV_ReShade_Backup_$stamp"
}

# Everything ReShade drops into \game\
$reshadeItems = @(
    'reshade-shaders',      # the shader graveyard
    'reshade-presets',      # presets
    'ReShade.ini',          # config (we re-apply the depth fix after)
    'ReShadePreset.ini',
    'dxgi.dll',             # the ReShade injector (one of these two, usually)
    'd3d11.dll',
    'dxgi.log',
    'd3d11.log',
    'ReShade.log',
    'ReShade_net.log',
    'ReShade.json',
    'ReShade64.json',
    'GShade.ini',
    'gshade-shaders',
    'gshade-presets'
)

Write-Step "Backing up current ReShade install -> $BackupPath"
$null = New-Item -ItemType Directory -Force -Path $BackupPath
$foundAny = $false
foreach ($item in $reshadeItems) {
    $src = Join-Path $game $item
    if (Test-Path $src) {
        $foundAny = $true
        if ($PSCmdlet.ShouldProcess($src, "Copy to backup")) {
            Copy-Item -Path $src -Destination $BackupPath -Recurse -Force
            Write-Ok "backed up: $item"
        }
    }
}
# Also grab any loose add-on files dropped in \game\
Get-ChildItem -Path $game -Filter '*.addon*' -File -ErrorAction SilentlyContinue | ForEach-Object {
    $foundAny = $true
    if ($PSCmdlet.ShouldProcess($_.FullName, "Copy to backup")) {
        Copy-Item $_.FullName -Destination $BackupPath -Force
        Write-Ok "backed up: $($_.Name)"
    }
}
if (-not $foundAny) {
    Write-Warn2 "No existing ReShade files found - fresh box, nothing to back up."
} else {
    Write-Ok "Backup complete. Keep this until the new install is confirmed working."
}

# -----------------------------------------------------------------------------
# 3. Delete the graveyard (clean slate - NEVER merge old shaders)
# -----------------------------------------------------------------------------
Write-Step "Removing old shaders/presets (clean slate)"
$wipeFolders = @('reshade-shaders','reshade-presets','gshade-shaders','gshade-presets')
foreach ($f in $wipeFolders) {
    $target = Join-Path $game $f
    if (Test-Path $target) {
        if ($PSCmdlet.ShouldProcess($target, "Delete")) {
            Remove-Item -Path $target -Recurse -Force
            Write-Ok "deleted: $f"
        }
    }
}
Write-Warn2 "Left the ReShade .dll/.ini in place. The installer (next step) refreshes them."
Write-Warn2 "For a 100% bare reinstall, also delete dxgi.dll/d3d11.dll + ReShade.ini by hand."

# -----------------------------------------------------------------------------
# 4. (Optional) Pull FREE iMMERSE + METEOR from Marty's GitHub - latest, matched
# -----------------------------------------------------------------------------
function Get-LatestGithubZip {
    param([string]$Repo, [string]$OutDir)
    $api = "https://api.github.com/repos/$Repo/releases/latest"
    $headers = @{ 'User-Agent' = 'ffxiv-reshade-helper'; 'Accept' = 'application/vnd.github+json' }
    try {
        $rel = Invoke-RestMethod -Uri $api -Headers $headers -ErrorAction Stop
    } catch {
        Write-Warn2 "Couldn't reach GitHub API for $Repo ($($_.Exception.Message)). Download manually."
        return $null
    }
    # Prefer a zip asset; fall back to the source zipball of the tagged release.
    $asset = $rel.assets | Where-Object { $_.name -like '*.zip' } | Select-Object -First 1
    $url   = if ($asset) { $asset.browser_download_url } else { $rel.zipball_url }
    $name  = if ($asset) { $asset.name } else { "$($Repo.Split('/')[-1])_$($rel.tag_name).zip" }
    $dest  = Join-Path $OutDir $name
    Write-Ok "$Repo -> release $($rel.tag_name)"
    Invoke-WebRequest -Uri $url -Headers $headers -OutFile $dest
    return $dest
}

if (-not $SkipMartyDownload) {
    Write-Step "Fetching version-matched iMMERSE + METEOR (free base) from GitHub"
    $dl = Join-Path $BackupPath '_downloads'
    $null = New-Item -ItemType Directory -Force -Path $dl
    if ($PSCmdlet.ShouldProcess('GitHub', 'Download iMMERSE + METEOR')) {
        # NOTE: confirm these repo slugs match Marty's current public repos first.
        $repos = @('martymcmodding/iMMERSE', 'martymcmodding/METEOR')
        foreach ($r in $repos) {
            $zip = Get-LatestGithubZip -Repo $r -OutDir $dl
            if ($zip) { Write-Ok "saved: $zip" }
        }
        Write-Warn2 "These are the FREE base shaders. Extract their Shaders\ + Textures\ into"
        Write-Warn2 "  $game\reshade-shaders\  AFTER ReShade + Alive NEXT are installed."
        Write-Warn2 "If a repo slug 404'd, use the green 'Code > Download ZIP' on Marty's GitHub."
    }
} else {
    Write-Warn2 "Skipping Marty downloads (-SkipMartyDownload)."
}

# -----------------------------------------------------------------------------
# 5. Apply the Dawntrail depth fix to ReShade.ini (if the ini exists)
# -----------------------------------------------------------------------------
function Set-DepthReversed {
    param([string]$IniPath)
    if (-not (Test-Path $IniPath)) {
        Write-Warn2 "ReShade.ini not present yet - apply the depth fix AFTER the installer (see guide)."
        return
    }
    $lines = Get-Content $IniPath
    $key   = 'RESHADE_DEPTH_INPUT_IS_REVERSED'
    if ($lines -match $key) {
        $lines = $lines -replace "($key)\s*=\s*\d", "`$1=1"
        Write-Ok "set $key=1"
    } else {
        Write-Warn2 "$key not in ini - set it in-game: Settings > Edit global preprocessor definitions > $key = 1"
    }
    Set-Content -Path $IniPath -Value $lines -Encoding UTF8
}

Write-Step "Applying Dawntrail depth fix"
if ($PSCmdlet.ShouldProcess((Join-Path $game 'ReShade.ini'), "Set depth reversed")) {
    Set-DepthReversed -IniPath (Join-Path $game 'ReShade.ini')
}

# -----------------------------------------------------------------------------
# Manual checklist
# -----------------------------------------------------------------------------
Write-Step "Automated steps done. Now the manual ones (in ORDER):"
Write-Todo "1. Run the ReShade setup .exe -> pick ffxiv_dx11.exe -> DirectX 10/11/12 ->"
Write-Todo "   TICK 'Install ReShade with full add-on support'. Use ReShade 6.7.3 or newer."
Write-Todo "2. Install the REST add-on (ReshadeEffectShaderToggler), enable it in the Add-ons tab."
Write-Todo "3. Install Alive NEXT: extract Alive_NEXT_WIP_xx.zip into  $game  (overwrite). Use the"
Write-Todo "   experimental/RTGI build since you own iMMERSE Pro. It needs iMMERSE 2506+."
Write-Todo "4. Drop FREE iMMERSE + METEOR (from $BackupPath\_downloads) into reshade-shaders\."
Write-Todo "5. Drop your FRESH Patreon iMMERSE Pro on top LAST so Pro == base version. Re-download it;"
Write-Todo "   do NOT reuse the stale copy that threw 'duplicate attribute' errors."
Write-Todo "6. In-game: confirm RESHADE_DEPTH_INPUT_IS_REVERSED = 1, put Launchpad/XIV_ImmPad at the"
Write-Todo "   TOP of the technique list, then enable the Alive NEXT preset."

Write-Host "`nBackup kept at: $BackupPath" -ForegroundColor Green
Write-Host "Done. Do NOT merge anything from the backup back into reshade-shaders - that's what broke it.`n" -ForegroundColor Green
