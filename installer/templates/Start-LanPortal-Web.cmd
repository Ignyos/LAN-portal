@echo off
setlocal
cd /d "%~dp0"
start "LanPortal Web" "%~dp0web\Ignyos.LanPortal.Web.exe" --urls "http://localhost:5014"
