@echo off
chcp 65001 >nul
cd /d "%~dp0\.."
echo === PharmaCore smoke tests (dev) ===
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0smoke-test-dev.ps1"
if errorlevel 1 exit /b 1
echo.
set /p RUN_E2E="Chay E2E (tao them du lieu test)? [y/N]: "
if /i "%RUN_E2E%"=="y" (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0smoke-test-e2e.ps1"
)
exit /b %ERRORLEVEL%
