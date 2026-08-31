@echo off
rem One command: exit running DSHW -> republish lite -> relaunch fresh build.
rem (cmd on non-CHS code pages can't render Chinese; keep echoed text ASCII.)
setlocal

echo === [1/3] Stop running DSHW ===
taskkill /IM DSHW.Desktop.exe /T /F >nul 2>&1
timeout /t 2 /nobreak >nul

echo === [2/3] Republish lite ===
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" -Target lite
if errorlevel 1 (
    echo Publish failed - not launching.
    pause
    exit /b 1
)

echo === [3/3] Launch lite ===
start "" "%~dp0..\DSHW.Desktop\release\lite\DSHW.Desktop.exe"

echo.
echo Done. If it did not auto-launch, run:
echo   DSHW.Desktop\release\lite\DSHW.Desktop.exe
pause
endlocal
