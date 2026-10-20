@echo off
setlocal

rem ============================================================================
rem  build.bat -- standalone build entry for this module (double-click it)
rem
rem  The real build logic lives in the sibling build.ps1; this wrapper only:
rem    1. Calls PowerShell with -ExecutionPolicy Bypass (avoids script policy errors)
rem    2. Forwards args: build.bat -SkipBuild -SkipImport -Rebuild
rem                       build.bat -Configuration Release
rem    3. Pauses at the end when run standalone; does NOT pause when invoked by
rem       the root one-click build (detected via STS2_BUILD_ALL=1) or when
rem       STS2_NO_PAUSE=1 is set
rem    4. Bubbles up the build.ps1 exit code (0 = OK, 1 = failed)
rem
rem  Note: keep this file pure ASCII with CRLF line endings -- the console
rem  prints it at the OEM code page, and any non-ASCII byte in a .bat gets
rem  mangled there. Chinese output comes from build.ps1 (PowerShell handles
rem  UTF-8 natively).
rem ============================================================================

set "SCRIPT=%~dp0build.ps1"
if not exist "%SCRIPT%" (
    echo [ERROR] build.ps1 not found: "%SCRIPT%"
    if not defined STS2_BUILD_ALL (
        if not defined STS2_NO_PAUSE pause
    )
    exit /b 1
)

echo ============================================================================
echo   Building %~dp0
echo ============================================================================
echo.

powershell -NoProfile -NoLogo -ExecutionPolicy Bypass -File "%SCRIPT%" %*
set "RC=%ERRORLEVEL%"

echo.
echo ----------------------------------------------------------------------------
if "%RC%"=="0" (
    echo [OK] BUILD OK: %~dp0
) else (
    echo [ERROR] BUILD FAILED: %~dp0 ^(exit code %RC%^)
)
echo ----------------------------------------------------------------------------

rem Only pause when run standalone; never pause under the one-click build
if not defined STS2_BUILD_ALL (
    if not defined STS2_NO_PAUSE (
        echo.
        pause
    )
)

endlocal & exit /b %RC%
