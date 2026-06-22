@echo off
chcp 65001 >nul
cd /d "%~dp0"
title PharmaCore Dev (API + Web)

echo === PharmaCore: API + Admin Web ===
echo API:  http://localhost:5290
echo Web:  http://localhost:5173
echo Login demo: admin / Admin@123
echo.

echo [1/4] Kiem tra PostgreSQL...
powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\check-postgres.ps1"
if errorlevel 1 (
    echo.
    echo Neu chua setup DB: chay scripts\setup-and-migrate.bat
    pause
    exit /b 1
)
echo.

where npm >nul 2>&1
if errorlevel 1 (
    echo [CANH BAO] Chua cai Node.js/npm. Cai tu: https://nodejs.org
    echo Chi chay API...
    start "PharmaCore API" cmd /k "dotnet run --project src\PharmaCore.Api\PharmaCore.Api.csproj --launch-profile http"
    goto :end
)

if not exist "client\admin\node_modules" (
    echo [2/4] npm install trong client\admin...
    pushd client\admin
    call npm install
    if errorlevel 1 (
        echo [LOI] npm install that bai
        popd
        pause
        exit /b 1
    )
    popd
)

echo [3/4] Dung API cu (neu co)...
taskkill /F /IM PharmaCore.Api.exe >nul 2>&1

echo [3/4] Build API...
dotnet build "src\PharmaCore.Api\PharmaCore.Api.csproj" --verbosity quiet
if errorlevel 1 (
    echo [LOI] Build API that bai
    pause
    exit /b 1
)

echo [3/4] Khoi dong API...
start "PharmaCore API" cmd /k "dotnet run --project src\PharmaCore.Api\PharmaCore.Api.csproj --launch-profile http"

timeout /t 3 /nobreak >nul

echo [4/4] Khoi dong Admin Web...
pushd client\admin
call npm run dev
popd

:end
pause
