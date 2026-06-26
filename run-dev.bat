@echo off

chcp 65001 >nul

cd /d "%~dp0"

title PharmaCore Dev (API + Admin + Customer)



echo === PharmaCore: mot lenh — API + Admin + Customer ===

echo API:      http://localhost:5290

echo Admin:    http://localhost:5173  (admin / Admin@123)

echo Customer: http://localhost:5174  (0909123456 / OTP 000000)

echo.

echo Watchdog tu khoi dong lai API neu mat ket noi (~8 giay).

echo.



where npm >nul 2>&1

if errorlevel 1 (

    echo [LOI] Can Node.js/npm: https://nodejs.org

    pause

    exit /b 1

)



powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\dev-all.ps1"

pause

