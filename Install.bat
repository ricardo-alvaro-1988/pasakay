@echo off
cd /d "%~dp0"
echo Installing Ya! Pasakay...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if errorlevel 1 (
  echo Install failed.
  pause
  exit /b 1
)
