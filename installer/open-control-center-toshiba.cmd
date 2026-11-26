@echo off
setlocal EnableExtensions
title Open TCS Control Center

rem Fixed target for the configured ASUS Choco AI node.
set "SSH_TARGET=asus@DESKTOP-GEKP3DR.local"

where ssh >nul 2>&1
if errorlevel 1 (
  echo OpenSSH Client tidak ditemukan di Toshiba.
  pause
  exit /b 1
)

where curl.exe >nul 2>&1
if errorlevel 1 (
  echo curl.exe tidak ditemukan di Toshiba.
  pause
  exit /b 1
)

echo Membuka SSH tunnel terenkripsi ke ASUS: %SSH_TARGET%
start "TCS ASUS Tunnel" cmd /k ssh -o ExitOnForwardFailure=yes -N -L 8765:127.0.0.1:8765 %SSH_TARGET%

echo Menunggu Remote Agent ASUS...
for /L %%I in (1,1,30) do (
  curl.exe -fsS --max-time 1 http://127.0.0.1:8765/health >nul 2>&1 && goto open_dashboard
  timeout /t 1 /nobreak >nul
)

echo.
echo Remote Agent belum menjawab pada port tunnel 8765.
echo Pastikan ASUS menyala, Remote Agent aktif, dan login SSH berhasil.
pause
exit /b 2

:open_dashboard
echo Koneksi tunnel siap. Membuka TCS Control Center...
start "" "http://127.0.0.1:8765/"
exit /b 0
