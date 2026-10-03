@echo off
setlocal

set "ROOT=%~dp0"
set "APP_EXE=%ROOT%My project.exe"
set "LOG_DIR=%ROOT%Logs"

rem ========================================
rem Application check
rem ========================================

if not exist "%APP_EXE%" (
    echo [ERROR] Application not found:
    echo %APP_EXE%
    pause
    exit /b 1
)

rem ========================================
rem Create log directory
rem ========================================

if not exist "%LOG_DIR%" (
    mkdir "%LOG_DIR%"
)

rem ========================================
rem Create log filename from startup time
rem ========================================

for /f %%i in ('powershell.exe -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss_fff"') do (
    set "TIMESTAMP=%%i"
)

set "LOG_FILE=%LOG_DIR%\Player_%TIMESTAMP%.log"

rem ========================================
rem Start Unity Player
rem ========================================

set "UNITY_PLAYER_LOG=%LOG_FILE%"

echo Starting application...
echo Log: %LOG_FILE%
echo.

start "" "%APP_EXE%" -logFile "%LOG_FILE%"

rem ========================================
rem Follow log in real time
rem ========================================

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command ^
    "$path = $env:UNITY_PLAYER_LOG;" ^
    "Write-Host ('Waiting for log: ' + $path);" ^
    "while (!(Test-Path -LiteralPath $path)) { Start-Sleep -Milliseconds 100 };" ^
    "Write-Host '--- Unity Player Log ---';" ^
    "Get-Content -LiteralPath $path -Wait"

endlocal