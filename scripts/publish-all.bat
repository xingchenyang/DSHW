@echo off
rem Publish all three variants (full / single / lite) to DSHW.Desktop\release\
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" %*
pause