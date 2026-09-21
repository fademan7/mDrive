@echo off
setlocal
cd /d "%~dp0"
"release\receiver\PhoneWheel.Receiver.exe" --usb --qr
if errorlevel 1 pause
