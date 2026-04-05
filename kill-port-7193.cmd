@echo off
REM Wrapper to run kill-port-7193.ps1 bypassing execution policy restrictions
powershell.exe -ExecutionPolicy Bypass -File "%~dp0kill-port-7193.ps1"
