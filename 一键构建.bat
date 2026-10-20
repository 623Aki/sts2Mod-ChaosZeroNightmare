@echo off
setlocal enabledelayedexpansion

rem ============================================================================
rem  One-click build entry for the whole workspace (this file)
rem
rem  Logic:
rem    1. Scan all Czn* (czn-prefixed, case-insensitive) subdirs of this dir
rem    2. Collect build.ps1 found in them, run them one by one, show results
rem    3. Prefer calling the sibling build.bat (reuses its pause / arg forwarding
rem       logic); if a dir has no build.bat, call build.ps1 via powershell
rem    4. A failing module does NOT abort the run; a summary is printed at the
rem       end and the overall exit code is 1 if any module failed
rem    5. Report Czn* dirs that have a csproj but no build.ps1 (silently skipped
rem       modules are the usual reason "my change didn't ship")
rem    6. Pause only when double-clicked; a run from a terminal or CI returns
rem       immediately. Set STS2_NO_PAUSE=1 to force no pause.
rem
rem  Usage:
rem    Double-click this file        -- build every Czn* module
rem    <this file> -SkipImport       -- extra args are forwarded to build.ps1
rem
rem  Child build.bat files detect STS2_BUILD_ALL=1 (set by this script) and
rem  skip their "pause" so the batch run stays unattended; double-clicking a
rem  build.bat on its own still pauses for result inspection.
rem ============================================================================

set "ROOT=%~dp0"
set "TOTAL=0"
set "IDX=0"
set "OKCOUNT=0"
set "FAILCOUNT=0"
set "FAILLIST="
set "SKIPPED="
set "EXITCODE=0"

rem Marker: tells child build.bat "invoked by build-all, do not pause"
set "STS2_BUILD_ALL=1"

rem Pause only when the file was launched fresh (Explorer double-click, or a
rem shell that starts cmd /c "<this file>"): in those cases the script name
rem shows up in cmdcmdline. An interactive cmd session keeps its own command
rem line (no script name), so it does not pause. Set STS2_NO_PAUSE=1 to force
rem no pause (handy for scripts / CI).
set "PAUSE_ON_EXIT="
echo "%cmdcmdline%" | find /i "%~nx0" >nul && set "PAUSE_ON_EXIT=1"
if defined STS2_NO_PAUSE set "PAUSE_ON_EXIT="

call :to_seconds T_START "%TIME: =0%"

echo ============================================================================
echo   STS2 Mod one-click build
echo   Scan dir: %ROOT%
echo ============================================================================
echo.

rem --- Count buildable Czn* dirs; remember csproj dirs without a build script ---
for /d %%D in ("%ROOT%Czn*") do (
    if exist "%%~fD\build.ps1" (
        set /a TOTAL+=1
    ) else (
        if exist "%%~fD\*.csproj" set "SKIPPED=!SKIPPED! %%~nxD"
    )
)

if "%TOTAL%"=="0" (
    echo [ERROR] No build.ps1 found under any Czn* dir, nothing to do.
    set "EXITCODE=1"
    goto :done
)

echo Found %TOTAL% build script^(s^):
for /d %%D in ("%ROOT%Czn*") do (
    if exist "%%~fD\build.ps1" echo     - %%~fD
)
if defined SKIPPED echo   [SKIP] csproj without build.ps1:%SKIPPED%
echo.

rem --- Run them one by one ---
for /d %%D in ("%ROOT%Czn*") do (
    if exist "%%~fD\build.ps1" (
        set /a IDX+=1
        echo ============================================================================
        echo   [!IDX!/%TOTAL%] Building %%~nxD
        echo ============================================================================
        echo.
        if exist "%%~fD\build.bat" (
            call "%%~fD\build.bat" %*
        ) else (
            powershell -NoProfile -NoLogo -ExecutionPolicy Bypass -File "%%~fD\build.ps1" %*
        )
        set "RC=!ERRORLEVEL!"
        if "!RC!"=="0" (
            set /a OKCOUNT+=1
            echo   ^>^> [%%~nxD] BUILD OK
        ) else (
            set /a FAILCOUNT+=1
            set "FAILLIST=!FAILLIST! %%~nxD"
            echo   ^>^> [%%~nxD] BUILD FAILED ^(exit code !RC!^)
        )
        echo.
    )
)

call :to_seconds T_END "%TIME: =0%"
set /a ELAPSED=T_END-T_START
if !ELAPSED! lss 0 set /a ELAPSED+=86400
set /a EMIN=ELAPSED/60
set /a ESEC=ELAPSED-EMIN*60

rem --- Summary ---
echo ============================================================================
echo   Summary: %OKCOUNT% OK, %FAILCOUNT% failed (of %TOTAL% total, took !EMIN!m !ESEC!s)
if not "%FAILCOUNT%"=="0" echo   Failed modules:%FAILLIST%
echo ============================================================================
echo.

if not "%FAILCOUNT%"=="0" (
    echo [ERROR] Some modules failed to build, check the output above.
    set "EXITCODE=1"
) else (
    echo [OK] All modules built successfully.
)

:done
echo.
if defined PAUSE_ON_EXIT pause
endlocal & exit /b %EXITCODE%

rem ============================================================================
rem  :to_seconds  --  convert HH:MM:SS.cc to seconds since midnight
rem     %1 = variable name to receive the value, %2 = time string
rem     The leading "1" makes 08 / 09 decimal instead of octal.
rem ============================================================================
:to_seconds
setlocal enabledelayedexpansion
set "SEC=0"
for /f "tokens=1-3 delims=:.," %%a in ("%~2") do (
    set /a "SEC=(1%%a-100)*3600+(1%%b-100)*60+(1%%c-100)"
)
endlocal & set "%~1=%SEC%"
exit /b 0
