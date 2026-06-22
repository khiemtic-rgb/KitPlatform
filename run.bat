@echo off
chcp 65001 >nul
cd /d "%~dp0"
title PharmaCore

echo === Khoi dong PharmaCore ===
echo Swagger: https://localhost:7xxx/swagger
echo.

dotnet run --project src\PharmaCore.Api\PharmaCore.Api.csproj
pause
