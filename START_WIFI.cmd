@echo off
setlocal
cd /d "%~dp0"
"release\receiver\PhoneWheel.Receiver.exe" --wifi --qr
if errorlevel 1 pause
