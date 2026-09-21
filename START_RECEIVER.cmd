@echo off
setlocal
cd /d "%~dp0"
if not exist "release\receiver\PhoneWheel.Receiver.exe" (
  echo Receiver executable not found. Build with tools\publish-receiver.ps1.
  pause
  exit /b 1
)
"release\receiver\PhoneWheel.Receiver.exe"
