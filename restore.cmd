@echo off
setlocal
powershell -ExecutionPolicy Bypass -File "%~dp0_internal\restore_rus.ps1"
exit /b %ERRORLEVEL%
