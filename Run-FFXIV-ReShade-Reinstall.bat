@echo off
setlocal
title FFXIV ReShade Clean Reinstall

REM ============================================================================
REM  Double-click launcher for ffxiv-reshade-clean-reinstall.ps1
REM  Keep this .bat in the SAME folder as the .ps1 file.
REM  It handles PowerShell + execution policy for you.
REM ============================================================================

cd /d "%~dp0"

set "PS1=%~dp0ffxiv-reshade-clean-reinstall.ps1"

if not exist "%PS1%" (
    echo.
    echo  [ERROR] Could not find:
    echo      %PS1%
    echo.
    echo  Make sure this .bat is in the SAME folder as the .ps1 script.
    echo.
    pause
    exit /b 1
)

:menu
echo.
echo  ============================================================
echo    FFXIV ReShade - Clean Reinstall
echo  ============================================================
echo.
echo    [1]  DRY RUN  (-WhatIf)  - shows what it WOULD do, changes nothing
echo    [2]  RUN FOR REAL        - backs up, wipes, downloads
echo    [3]  Quit
echo.
set "choice="
set /p "choice=  Pick 1, 2, or 3 then press Enter: "

if "%choice%"=="1" goto dryrun
if "%choice%"=="2" goto realrun
if "%choice%"=="3" exit /b 0
echo  Invalid choice, try again.
goto menu

:dryrun
echo.
echo  --- DRY RUN (nothing will be deleted) ---
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%" -WhatIf
goto done

:realrun
echo.
echo  This will BACK UP, then DELETE your old reshade-shaders/presets.
set "ok="
set /p "ok=  Type YES to continue: "
if /i not "%ok%"=="YES" (
    echo  Cancelled.
    goto done
)
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%"
goto done

:done
echo.
echo  ============================================================
echo    Finished. Read the output above.
echo  ============================================================
echo.
pause
endlocal
