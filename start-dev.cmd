@echo off
rem Starts PostgreSQL (if needed), migrations, backend and frontend. Options: -SkipMigrations -NoBrowser
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-Dev.ps1" %*
if errorlevel 1 pause
