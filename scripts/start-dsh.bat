@echo off
rem Start DSH web manually (console window, Ctrl+C to stop)
rem Same as DSHW internal: npx -y @deepseek-ai/dsh web
cd /d "%~dp0.."
echo Starting DSH web: http://127.0.0.1:3080  (Ctrl+C to stop)
echo.
npx -y @deepseek-ai/dsh web
echo.
echo DSH stopped (exit code %ERRORLEVEL%)
pause