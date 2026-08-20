@echo off
rem Kill leftover DSH on 127.0.0.1:3080 (shows target and asks confirm; safe guard against killing Harness)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0kill-dsh.ps1" %*
pause