@echo off
setlocal
set "URL=http://127.0.0.1:5000"
set "HOST=%~dp0VibeDeck.Host.exe"

if not exist "%HOST%" (
  echo VibeDeck Host is missing. Re-run VibeDeck Setup.
  exit /b 1
)

start "" /b "%HOST%" >nul 2>&1
rem A second launch exits through the single-instance guard when Host is already running.
timeout /t 2 /nobreak >nul
start "" "%URL%"
exit /b 0
