@echo off
:: Change directory to the root directory where this script is located
cd /d "%~dp0"

title VibeDeck Host Launcher

echo ==========================================================
echo            VibeDeck Host Service Launcher
echo ==========================================================
echo.
echo Checking environment...

:: 1. Check if dotnet CLI is available (system install or user-local install)
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    if not exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
        echo [ERROR] .NET 8 SDK is not installed.
        echo Download link: https://dotnet.microsoft.com/download/dotnet/8.0
        echo.
        pause
        exit /b 1
    )
)

:: 2. Check if the project file exists
if not exist "src\PhoneMonitor.Host\PhoneMonitor.Host.csproj" (
    echo [ERROR] Could not find src\PhoneMonitor.Host\PhoneMonitor.Host.csproj.
    echo Please ensure this BAT file is run from the root of the phoneMonitor directory.
    echo.
    pause
    exit /b 1
)

echo Environment check passed.
echo.
echo Launching the service (this will setup HTTPS certificates if needed)...
echo.

:: Run the dev-run.ps1 script using PowerShell
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\dev-run.ps1"

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] The service exited with error code: %errorlevel%.
    pause
)
