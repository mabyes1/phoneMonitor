@echo off
setlocal
set "SERVICE=VibeDeckHost"
set "URL=http://127.0.0.1:5000"

sc query "%SERVICE%" >nul 2>&1
if errorlevel 1 (
  echo VibeDeck Host service is not installed.
  echo Re-run VibeDeck Setup, or start PhoneMonitor.Host.exe manually.
  pause
  exit /b 1
)

sc query "%SERVICE%" | findstr /I "RUNNING" >nul
if errorlevel 1 (
  echo Starting VibeDeck Host service...
  net start "%SERVICE%" >nul 2>&1
  if errorlevel 1 (
    echo Could not start the service automatically. Trying once more after elevation may be required.
    timeout /t 1 /nobreak >nul
    net start "%SERVICE%" >nul 2>&1
  )
  rem Give Kestrel a moment to bind ports.
  timeout /t 2 /nobreak >nul
)

start "" "%URL%"
exit /b 0
