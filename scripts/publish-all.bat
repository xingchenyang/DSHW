@echo off
rem Publish all variants (full / lite) to DSHW.Desktop\release\
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" %*
pause