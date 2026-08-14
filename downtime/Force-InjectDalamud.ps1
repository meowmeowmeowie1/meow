<#
.SYNOPSIS
    Force-load the snapshotted Dalamud runtime (with MyTweak) into a freshly
    patched FFXIV client, before goatcorp officially re-enables Dalamud.

.DESCRIPTION
    After a game patch, XIVLauncher's version gate disables Dalamud until a new
    build is whitelisted for the new game version. That gate lives ONLY in
    XIVLauncher -- Dalamud itself and Dalamud.Injector.exe do not check the game
    version. This script takes the runtime that MyTweak-OfflineKit.ps1 snapshotted
    before downtime (or the live Hooks\dev runtime) and injects it into the running
    game, so MyTweak (a dev plugin) and your other plugins load on the new patch.

    WARNING: this runs an OLD Dalamud against a NEW game binary. Memory signatures
    move between patches, so this can crash the client -- usually fine on small
    hotfixes, often not on major patches. goatcorp gate it for exactly this reason.
    The worst case is a crash to desktop; nothing is permanently damaged, and your
    OfflineKit snapshots are untouched. You are on your own doing this.

    RECOMMENDED FLOW (Inject mode, the default):
      1. Before downtime, run MyTweak-OfflineKit.ps1 so a snapshot + the MyTweak
         devPlugin exist.
      2. After the patch, launch the game via XIVLauncher's "Start w/o Dalamud".
      3. Log in and reach the character-select screen or your character.
      4. Run:  .\Force-InjectDalamud.ps1 -SafeMode
         (SafeMode does a first pass with third-party plugins off; MyTweak, being a
         dev plugin, still loads. If that's stable, restart the game and run the
         plain .\Force-InjectDalamud.ps1 for everything.)

.PARAMETER Launch
    Alternative route: instead of injecting into a running game, start XIVLauncher
    with --dalamud-runner-override pointed at the snapshot's Dalamud.Injector.exe,
    so XL's normal launch flow injects the old runtime on the unsupported version.
    Injects at launch (entrypoint), which is the crash-prone moment -- Inject mode
    is preferred. Close any running XIVLauncher first (it is single-instance).

.PARAMETER Runtime
    Explicit path to a Dalamud runtime folder to inject (must contain
    Dalamud.Injector.exe and Dalamud.dll). Overrides auto-selection.

.PARAMETER GamePath
    Override the FFXIV install root (the folder containing 'game\'). Auto-detected
    from XIVLauncher's config otherwise. Only used for the preflight version check.

.PARAMETER SafeMode
    First-pass stability check: adds --no-3rd-plugin (third-party plugins off).
    MyTweak still loads as a dev plugin.

.PARAMETER BareMode
    Escalation past SafeMode: adds --no-plugin (NO plugins at all) to test whether
    the Dalamud core itself survives on the new patch. Mutually exclusive with
    -SafeMode.

.PARAMETER Language
    Client language for Dalamud (Japanese/English/German/French). Auto-detected
    from launcherConfigV3.json, default English.

.PARAMETER ProcessId
    Inject into these ffxiv_dx11 PID(s) instead of auto-detecting.

.PARAMETER FixAcl
    Adds --fix-acl. First thing to try if injection fails with an access error.

.PARAMETER SeDebugPrivilege
    Adds --se-debug-privilege. Try (from an elevated PowerShell) if OpenProcess
    still fails after -FixAcl.

.PARAMETER DelayInitializeMs
    Adds --dalamud-delay-initialize=<ms>. Escalation for load-order races.

.PARAMETER DalamudConsole
    Adds --console --crash-handler-console -v for live diagnostics.

.PARAMETER SkipPreflight
    Skip the release-API check that would otherwise stop early if the official
    Dalamud already supports your game version (i.e. no forcing needed).

.PARAMETER Force
    Skip confirmation prompts and the already-injected guard.

.EXAMPLE
    .\Force-InjectDalamud.ps1 -SafeMode
    Recommended first attempt after a patch, from character select.

.EXAMPLE
    .\Force-InjectDalamud.ps1
    Full inject (all plugins) once SafeMode proved stable.

.EXAMPLE
    .\Force-InjectDalamud.ps1 -Launch
    Let XIVLauncher inject the snapshot at launch instead.
#>

[CmdletBinding(DefaultParameterSetName = 'Inject')]
param(
    [string]$Runtime,
    [string]$GamePath,
    [switch]$SkipPreflight,
    [switch]$Force,

    [Parameter(ParameterSetName = 'Inject')][switch]$SafeMode,
    [Parameter(ParameterSetName = 'Inject')][switch]$BareMode,
    [Parameter(ParameterSetName = 'Inject')][ValidateSet('Japanese', 'English', 'German', 'French')][string]$Language,
    [Parameter(ParameterSetName = 'Inject')][int[]]$ProcessId,
    [Parameter(ParameterSetName = 'Inject')][switch]$FixAcl,
    [Parameter(ParameterSetName = 'Inject')][switch]$SeDebugPrivilege,
    [Parameter(ParameterSetName = 'Inject')][int]$DelayInitializeMs = 0,
    [Parameter(ParameterSetName = 'Inject')][switch]$DalamudConsole,

    [Parameter(ParameterSetName = 'Launch', Mandatory = $true)][switch]$Launch
)

$ErrorActionPreference = 'Stop'

# ---------- Paths / constants ----------
$XLRoot            = Join-Path $env:APPDATA 'XIVLauncher'
$HooksRoot         = Join-Path $XLRoot 'addon\Hooks'
$DalamudDev        = Join-Path $HooksRoot 'dev'
$RuntimeDotnetRoot = Join-Path $XLRoot 'runtime'
$AssetDir          = Join-Path $XLRoot 'dalamudAssets\dev'
$ConfigPath        = Join-Path $XLRoot 'dalamudConfig.json'
$PluginDir         = Join-Path $XLRoot 'installedPlugins'
$LauncherConfig    = Join-Path $XLRoot 'launcherConfigV3.json'
$SnapshotPattern   = 'dev-mytweak-snapshot-*'
$XLExe             = Join-Path $env:LOCALAPPDATA 'XIVLauncher\XIVLauncher.exe'
$DalamudVersionUrl = 'https://kamori.goats.dev/Dalamud/Release/VersionInfo?track=release'
$GameProcessName   = 'ffxiv_dx11'
$LogDir            = Join-Path $PSScriptRoot 'logs'
$LogName           = 'forceinject'

$LangCodes = @{ 'Japanese' = 0; 'English' = 1; 'German' = 2; 'French' = 3 }

# ---------- Output helpers (house style) ----------
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

function Confirm-Action($message) {
    if ($Force) { return $true }
    Write-Host ''
    Write-Host $message -ForegroundColor Yellow
    Write-Host 'Proceed? [Y/N] ' -NoNewline -ForegroundColor Yellow
    $response = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown').Character
    Write-Host $response
    return ($response -eq 'Y' -or $response -eq 'y')
}

function Assert-XIVLauncher {
    if (-not (Test-Path $XLRoot)) {
        Write-Fail "XIVLauncher folder not found at: $XLRoot"
        Write-Fail "Install XIVLauncher and launch it once before running this kit."
        exit 1
    }
}

function Read-JsonFile($path) {
    if (-not (Test-Path $path)) { return $null }
    try {
        # Strip BOM; PS 5.1 ConvertFrom-Json chokes on it.
        $raw = (Get-Content $path -Raw).TrimStart([char]0xFEFF)
        return ($raw | ConvertFrom-Json)
    } catch { return $null }
}

function Get-DalamudVersion($dir) {
    $dll = Join-Path $dir 'Dalamud.dll'
    if (-not (Test-Path $dll)) { return $null }
    try { return (Get-Item $dll).VersionInfo.FileVersion } catch { return 'unknown' }
}

# ---------- Game version detection (from Watch-FFXIVPlugins.ps1) ----------
function Get-GameRoot {
    if ($GamePath) {
        if (Test-Path (Join-Path $GamePath 'game\ffxivgame.ver')) { return $GamePath }
        return $null
    }
    $cfg = Read-JsonFile $LauncherConfig
    if ($cfg -and $cfg.GamePath -and (Test-Path (Join-Path $cfg.GamePath 'game\ffxivgame.ver'))) {
        return $cfg.GamePath
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

# ---------- Preflight: is forcing even necessary? ----------
function Test-OfficialDalamudReady($gameVer) {
    try {
        $info = Invoke-RestMethod -Uri $DalamudVersionUrl -UseBasicParsing -TimeoutSec 20
    } catch {
        Write-Warn "Could not reach the Dalamud release API ($($_.Exception.Message)). Proceeding."
        return
    }
    if ($gameVer -and $info.supportedGameVer -eq $gameVer) {
        Write-OK "Official Dalamud (v$($info.assemblyVersion)) already supports your game version ($gameVer)."
        Write-OK "No forcing needed -- close the game and launch normally through XIVLauncher."
        Write-Info "Run with -SkipPreflight if you want to force anyway."
        exit 0
    }
    if ($gameVer) {
        Write-Info "Official Dalamud supports $($info.supportedGameVer); you are on $gameVer. Forcing is applicable."
    }
}

# ---------- Runtime selection ----------
function Get-RuntimeCandidates {
    $list = @()
    if (Test-Path $DalamudDev) {
        $list += [PSCustomObject]@{ Label = 'Hooks\dev (live)'; Path = $DalamudDev }
    }
    $snaps = Get-ChildItem $HooksRoot -Directory -ErrorAction SilentlyContinue |
             Where-Object { $_.Name -like $SnapshotPattern } | Sort-Object Name -Descending
    foreach ($s in $snaps) {
        $list += [PSCustomObject]@{ Label = $s.Name; Path = $s.FullName }
    }
    foreach ($c in $list) {
        $c | Add-Member NoteProperty HasInjector (Test-Path (Join-Path $c.Path 'Dalamud.Injector.exe'))
        $c | Add-Member NoteProperty HasDalamud  (Test-Path (Join-Path $c.Path 'Dalamud.dll'))
        $c | Add-Member NoteProperty DalamudVer  (Get-DalamudVersion $c.Path)
        $vj = Read-JsonFile (Join-Path $c.Path 'version.json')
        $supported = if ($vj -and $vj.SupportedGameVer) { $vj.SupportedGameVer } else { 'unknown' }
        $c | Add-Member NoteProperty SupportedGameVer $supported
    }
    return $list
}

function Select-Runtime {
    if ($Runtime) {
        if (-not (Test-Path (Join-Path $Runtime 'Dalamud.Injector.exe'))) {
            Write-Fail "-Runtime '$Runtime' has no Dalamud.Injector.exe."
            exit 1
        }
        Write-Info "Using explicit runtime: $Runtime"
        return $Runtime
    }

    $cands = @(Get-RuntimeCandidates)
    $valid = @($cands | Where-Object { $_.HasInjector -and $_.HasDalamud })

    Write-Host ''
    Write-Host 'Available Dalamud runtimes:' -ForegroundColor Cyan
    if ($cands.Count -eq 0) {
        Write-Host '  (none found)'
    } else {
        $cands | ForEach-Object {
            $ok = if ($_.HasInjector -and $_.HasDalamud) { 'usable ' } else { 'INCOMPLETE' }
            Write-Host ("  [{0}] {1,-34} Dalamud {2}  supports {3}" -f $ok, $_.Label, $_.DalamudVer, $_.SupportedGameVer)
        }
    }
    Write-Host ''

    if ($valid.Count -eq 0) {
        Write-Fail "No usable runtime (need Dalamud.Injector.exe + Dalamud.dll)."
        Write-Fail "Run MyTweak-OfflineKit.ps1 BEFORE the next patch so a snapshot exists,"
        Write-Fail "or launch XIVLauncher once while Dalamud still works to populate Hooks\dev."
        exit 1
    }

    $chosen = $valid[0]
    Write-Info "Selected runtime: $($chosen.Label)  (Dalamud $($chosen.DalamudVer))"
    if ($valid.Count -gt 1) {
        Write-Info "Other usable runtimes exist; pass -Runtime <path> to pick a different one."
    }
    return $chosen.Path
}

# ---------- Client language ----------
function Get-ClientLanguageCode {
    if ($Language) { return $LangCodes[$Language] }
    $cfg = Read-JsonFile $LauncherConfig
    if ($cfg) {
        # XIVLauncher may store this as 'ClientLanguage' or 'Language', and as an
        # enum name ("English") or an int (0-3). Accept all forms.
        foreach ($field in @('ClientLanguage', 'Language')) {
            $val = $cfg.$field
            if ($null -eq $val) { continue }
            if ($val -is [int] -and $val -ge 0 -and $val -le 3) { return [int]$val }
            $s = [string]$val
            if ($LangCodes.ContainsKey($s)) { return $LangCodes[$s] }
            $n = 0
            if ([int]::TryParse($s, [ref]$n) -and $n -ge 0 -and $n -le 3) { return $n }
        }
    }
    Write-Info "Client language not detected; defaulting to English. Use -Language to override."
    return 1
}

# ---------- Game process discovery ----------
function Get-GameProcesses {
    if ($ProcessId) {
        $procs = @()
        foreach ($id in $ProcessId) {
            try {
                $p = Get-Process -Id $id -ErrorAction Stop
                if ($p.ProcessName -ne $GameProcessName) {
                    Write-Warn "PID $id is '$($p.ProcessName)', not $GameProcessName -- skipping."
                    continue
                }
                $procs += $p
            } catch {
                Write-Warn "PID $id not found -- skipping."
            }
        }
        return $procs
    }
    return @(Get-Process -Name $GameProcessName -ErrorAction SilentlyContinue)
}

function Test-AlreadyInjected($proc) {
    try {
        foreach ($m in $proc.Modules) {
            if ($m.ModuleName -eq 'Dalamud.Boot.dll') { return $true }
        }
        return $false
    } catch {
        return $null  # access denied / cannot enumerate -> unknown
    }
}

function Test-IsElevated {
    try {
        $id = [Security.Principal.WindowsIdentity]::GetCurrent()
        return ([Security.Principal.WindowsPrincipal]$id).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    } catch { return $false }
}

# ---------- Injector invocation ----------
function Get-InjectorExe($runtimePath) {
    $exe = Join-Path $runtimePath 'Dalamud.Injector.exe'
    if (-not (Test-Path $exe)) {
        Write-Fail "Dalamud.Injector.exe missing from $runtimePath"
        exit 1
    }
    return $exe
}

function Set-DalamudEnv {
    # Mirror how XIVLauncher launches the injector: framework-dependent .NET app
    # that resolves its runtime from %APPDATA%\XIVLauncher\runtime.
    $env:DOTNET_ROOT = $RuntimeDotnetRoot
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
}

function Test-InjectorSanity($injectorExe, $runtimePath) {
    Set-DalamudEnv
    try {
        Push-Location $runtimePath
        & $injectorExe 'help' 2>&1 | Out-Null
        Pop-Location
        return $true
    } catch {
        try { Pop-Location } catch { }
        Write-Fail "The injector would not start standalone: $($_.Exception.Message)"
        Write-Fail "The bundled .NET runtime under $RuntimeDotnetRoot may be missing."
        return $false
    }
}

function Build-InjectorArgs($runtimePath, $pids, $langCode) {
    $a = @('inject')
    foreach ($p in $pids) { $a += "$p" }
    $a += "--dalamud-working-directory=$runtimePath"
    $a += "--dalamud-configuration-path=$ConfigPath"
    $a += "--dalamud-plugin-directory=$PluginDir"
    $a += "--dalamud-asset-directory=$AssetDir"
    $a += "--dalamud-client-language=$langCode"
    $a += "--logpath=$LogDir"
    $a += "--logname=$LogName"
    if ($SafeMode)              { $a += '--no-3rd-plugin' }
    if ($BareMode)              { $a += '--no-plugin' }
    if ($FixAcl)                { $a += '--fix-acl' }
    if ($SeDebugPrivilege)      { $a += '--se-debug-privilege' }
    if ($DelayInitializeMs -gt 0) { $a += "--dalamud-delay-initialize=$DelayInitializeMs" }
    if ($DalamudConsole)        { $a += @('--console', '--crash-handler-console', '-v') }
    return $a
}

function Invoke-Injector($injectorExe, $runtimePath, $argList) {
    Set-DalamudEnv
    Write-Info ("Running: Dalamud.Injector.exe " + ($argList -join ' '))
    try {
        Push-Location $runtimePath
        & $injectorExe @argList
        $code = $LASTEXITCODE
    } finally {
        try { Pop-Location } catch { }
    }
    return $code
}

function Resolve-InjectorExitCode($code) {
    # PS 5.1 can surface -1 as an unsigned wrap; normalise.
    $c = $code
    if ($c -eq 4294967295) { $c = -1 }
    if ($c -eq 4294967294) { $c = -2 }
    switch ($c) {
        0       { Write-OK   "Injector reported success (exit 0)."; return $true }
        -1      { Write-Fail "Injector found no target process (exit -1) -- did the game close?"; return $false }
        -2      { Write-Warn "Injection cancelled at the injector prompt (exit -2)."; return $false }
        default { Write-Warn "Injector exit code $code -- check the log in $LogDir."; return $false }
    }
}

# ---------- Inject mode ----------
function Invoke-InjectMode {
    Write-Header "MyTweak Force-Inject -- Inject into running game"
    Assert-XIVLauncher

    if ($SafeMode -and $BareMode) {
        Write-Fail "-SafeMode and -BareMode are mutually exclusive."
        exit 1
    }

    $gameVer = Get-GameVersion
    if (-not $SkipPreflight) { Test-OfficialDalamudReady $gameVer }

    $runtimePath = Select-Runtime
    $injectorExe = Get-InjectorExe $runtimePath
    $langCode    = Get-ClientLanguageCode

    $procs = @(Get-GameProcesses)
    if ($procs.Count -eq 0) {
        Write-Fail "No $GameProcessName process found."
        Write-Fail "Launch the game via XIVLauncher's 'Start w/o Dalamud', reach character"
        Write-Fail "select or your character, then re-run this script."
        exit 1
    }
    Write-Info "Target process(es):"
    foreach ($p in $procs) {
        $started = try { $p.StartTime.ToString('HH:mm:ss') } catch { '??:??:??' }
        Write-Host ("    PID {0}  (started {1})" -f $p.Id, $started)
        $inj = Test-AlreadyInjected $p
        if ($inj -eq $true -and -not $Force) {
            Write-Fail "PID $($p.Id) already has Dalamud injected. Double-injection is unguarded"
            Write-Fail "and will likely crash the client. Restart the game, or pass -Force."
            exit 1
        } elseif ($null -eq $inj) {
            Write-Warn "  (could not read modules for PID $($p.Id) -- injection state unknown)"
        }
    }

    if ($SeDebugPrivilege -and -not (Test-IsElevated)) {
        Write-Warn "-SeDebugPrivilege usually requires an elevated (admin) PowerShell; it may fail otherwise."
    }

    if (-not (Test-InjectorSanity $injectorExe $runtimePath)) { exit 1 }

    $modeNote = if ($SafeMode) { " (SafeMode: third-party plugins off)" }
                elseif ($BareMode) { " (BareMode: NO plugins)" } else { "" }
    $warn = @"
About to force-inject an OLD Dalamud into the CURRENT (possibly just-patched) game$modeNote.

  - This runs Dalamud built for a different game version. If memory signatures moved
    (common on major patches), the client can CRASH -- typically at the SE logo/title,
    which is why injecting from character-select is safer.
  - goatcorp's stance: if you bypass the post-patch disable, you are on your own.
  - Nothing is permanently damaged; a crash just means wait for the official update.
  - First time on a new patch, prefer -SafeMode.
"@
    if (-not (Confirm-Action $warn)) { Write-Info "Cancelled."; return }

    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    $pids = $procs | ForEach-Object { $_.Id }
    $argList = Build-InjectorArgs $runtimePath $pids $langCode
    $code = Invoke-Injector $injectorExe $runtimePath $argList
    $ok = Resolve-InjectorExitCode $code

    if ($ok) {
        Write-Host ''
        Write-OK "Injection command completed. Alt-tab to the game -- Dalamud + MyTweak should load."
        Write-Info "If /xlplugins shows MyTweak in the Dev tab, you're set."
        if ($SafeMode) {
            Write-Info "This was a SafeMode pass. To load everything, RESTART the game (do not"
            Write-Info "re-inject the same session) and run: .\Force-InjectDalamud.ps1"
        }
        Write-Info "Log: $LogDir"
    } else {
        Write-Host ''
        Write-Warn "Injection did not confirm success. Escalation ladder:"
        Write-Host "    1) .\Force-InjectDalamud.ps1 -FixAcl"
        Write-Host "    2) (elevated PowerShell) .\Force-InjectDalamud.ps1 -FixAcl -SeDebugPrivilege"
        Write-Host "    3) .\Force-InjectDalamud.ps1 -SafeMode   (rule out a bad 3rd-party plugin)"
        Write-Host "    4) .\Force-InjectDalamud.ps1 -DalamudConsole   (watch the load live)"
        Write-Host "    5) Inspect the newest log under $LogDir"
    }
}

# ---------- Launch mode ----------
function Invoke-LaunchMode {
    Write-Header "MyTweak Force-Inject -- XIVLauncher runner override"
    Assert-XIVLauncher

    $gameVer = Get-GameVersion
    if (-not $SkipPreflight) { Test-OfficialDalamudReady $gameVer }

    $runtimePath = Select-Runtime
    $injectorExe = Get-InjectorExe $runtimePath

    if (-not (Test-Path $XLExe)) {
        Write-Fail "XIVLauncher.exe not found at $XLExe"
        exit 1
    }
    if (@(Get-Process -Name 'XIVLauncher' -ErrorAction SilentlyContinue).Count -gt 0) {
        Write-Fail "XIVLauncher is already running. Close it first (it is single-instance), then re-run."
        exit 1
    }

    $warn = @"
About to start XIVLauncher with --dalamud-runner-override so it injects the OLD Dalamud
at launch, on a possibly just-patched game.

  - Launch-time (entrypoint) injection is the crash-prone path; Inject mode (the default,
    from character-select) is safer. Use this only if you prefer the hands-off route.
  - The override applies to THIS launch only; future launches are unaffected.
  - You are on your own bypassing the post-patch disable.
"@
    if (-not (Confirm-Action $warn)) { Write-Info "Cancelled."; return }

    $overrideArg = "--dalamud-runner-override=$injectorExe"
    Write-Info "Starting: XIVLauncher.exe $overrideArg"
    Start-Process -FilePath $XLExe -ArgumentList $overrideArg
    Write-OK "XIVLauncher started with the runner override. Log in as normal."
    Write-Info "Dalamud logs for this route are managed by XL under $XLRoot (dalamud.log)."
}

# ---------- Dispatch ----------
switch ($PSCmdlet.ParameterSetName) {
    'Launch' { Invoke-LaunchMode }
    default  { Invoke-InjectMode }
}
