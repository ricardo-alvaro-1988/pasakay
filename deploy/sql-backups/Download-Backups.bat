@echo off
setlocal
echo Downloads backups and deletes server copies ONLY after verification.
echo Keep another copy of your downloaded backups on separate storage.
echo.
if not exist "%~dp0Download-Backups.ps1" (
    echo ERROR: Download-Backups.ps1 must be in the same folder as this launcher.
    pause
    exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Download-Backups.ps1"
set "backupExitCode=%ERRORLEVEL%"
echo.
if not "%backupExitCode%"=="0" echo Backup download reported an error. Review the messages above.
pause
exit /b %backupExitCode%
