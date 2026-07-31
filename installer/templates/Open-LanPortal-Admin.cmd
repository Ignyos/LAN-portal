@echo off
set "SCRIPT_DIR=%~dp0"
if exist "%SCRIPT_DIR%host\Ignyos.LanPortal.Host.exe" (
	start "" "%SCRIPT_DIR%host\Ignyos.LanPortal.Host.exe"
) else (
	start "" "http://localhost:5212/local/admin"
)
