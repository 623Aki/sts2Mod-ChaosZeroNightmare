@echo off
rem Double-click wrapper for build.ps1 (kept ASCII-only so cmd does not mis-decode it).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
if errorlevel 1 (
    echo.
    echo Build FAILED.
) else (
    echo.
    echo Build OK.
)
pause
