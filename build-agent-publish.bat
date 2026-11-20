@echo off
chcp 65001 >nul
cd /d "%~dp0"
title KitPlatform - build Agent dang bai
powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\build-agent-publish.ps1"
echo.
pause
