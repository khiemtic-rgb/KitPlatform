@echo off
chcp 65001 >nul
cd /d "%~dp0\.."
echo === KitPlatform: Tao DB local kitplatform ===
echo.
echo Can mat khau superuser postgres (dat luc cai PostgreSQL).
echo Script se clone tu pharmacore neu co, giu du lieu dev.
echo.
set /p PGPASS="Nhap mat khau postgres: "
powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\setup-kitplatform-local.ps1" -PostgresPassword "%PGPASS%"
pause
