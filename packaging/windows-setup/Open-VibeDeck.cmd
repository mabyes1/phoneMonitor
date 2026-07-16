@echo off
setlocal EnableExtensions
set "URL=http://127.0.0.1:5000"
set "HEALTH=%URL%/health"
set "HOST=%~dp0VibeDeck.Host.exe"

if not exist "%HOST%" (
  echo VibeDeck Host is missing. Re-run VibeDeck Setup.
  pause
  exit /b 1
)

start "" /b "%HOST%" >nul 2>&1
rem A second launch exits through the single-instance guard when Host is already running.

set "READY=0"
for /L %%i in (1,1,40) do (
  powershell -NoProfile -Command "try { $r = Invoke-WebRequest -UseBasicParsing -Uri '%HEALTH%' -TimeoutSec 1; if ($r.StatusCode -eq 200) { exit 0 } else { exit 1 } } catch { exit 1 }" >nul 2>&1
  if not errorlevel 1 (
    set "READY=1"
    goto :open
  )
  timeout /t 1 /nobreak >nul
)

:open
if "%READY%"=="0" (
  echo VibeDeck Host did not become ready on port 5000.
  echo Check that nothing else owns the port, then open:
  echo   %URL%
  echo Logs: %%ProgramData%%\VibeDeck\logs  ^(installed^) or %%LocalAppData%%\PhoneMonitor\logs  ^(dev^)
  pause
  exit /b 2
)

start "" "%URL%"
exit /b 0
