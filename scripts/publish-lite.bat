@echo off
rem Publish LITE folder version (FDD, needs .NET 9 + WinAppSDK Runtime) -> release\lite\
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" -Target lite
pause