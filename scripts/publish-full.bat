@echo off
rem Publish FULL version (SCD single-file, no runtime needed) -> release\full\
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" -Target full
pause