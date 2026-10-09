@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Installeren.ps1"
if errorlevel 1 (
  echo Installatie mislukt. Zie de melding hierboven.
) else (
  echo Klaar. Start AutoCAD of Civil 3D opnieuw en voer TBLOCKS uit.
)
pause
