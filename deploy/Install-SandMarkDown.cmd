@echo off
set ARGS=%*
if /I "%~1"=="/?" set ARGS=-Help
if /I "%~1"=="-?" set ARGS=-Help
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-SandMarkDown.ps1" %ARGS%
