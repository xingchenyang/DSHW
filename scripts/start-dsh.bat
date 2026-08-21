@echo off
rem Start DSH web manually (console window, Ctrl+C to stop)
rem Uses the globally installed dsh command (offline, fast).
rem To use npx -y auto-update instead, run:  set DSHW_USE_NPX=1
cd /d "%~dp0.."
echo Starting DSH web: http://127.0.0.1:3080  (Ctrl+C to stop)
echo.
if "%DSHW_USE_NPX%"=="1" (
    npx -y @deepseek-ai/dsh web
) else (
    dsh web
)
echo.
echo DSH stopped (exit code %ERRORLEVEL%)
pause