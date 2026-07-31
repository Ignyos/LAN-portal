@echo off
setlocal
cd /d "%~dp0"
start "LanPortal API" "%~dp0api\Ignyos.LanPortal.Api.exe" --urls "http://localhost:5212"
