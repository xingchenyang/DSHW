@echo off
rem Publish SINGLE version (WinAppSDK self-contained, needs .NET 9) -> release\single\
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" -Target single
pause