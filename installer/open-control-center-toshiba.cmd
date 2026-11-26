@echo off
setlocal EnableExtensions
title Open TCS Control Center

set "TARGET_FILE=%USERPROFILE%\.tcs-asus-ssh-target"
set "SSH_TARGET="

if exist "%TARGET_FILE%" set /p SSH_TARGET=<"%TARGET_FILE%"
if not defined SSH_TARGET (
  echo Masukkan target SSH ASUS yang sudah pernah berhasil dipakai.
  echo Contoh format: user@hostname.local
  set /p SSH_TARGET=Target SSH ASUS: 
  if not defined SSH_TARGET exit /b 1
  >"%TARGET_FILE%" echo %SSH_TARGET%
)

echo.
echo Membuka SSH tunnel terenkripsi ke ASUS...
start "TCS ASUS Tunnel" cmd /k ssh -o ExitOnForwardFailure=yes -N -L 8765:127.0.0.1:8765 %SSH_TARGET%

echo Menunggu Remote Agent ASUS...
for /L %%I in (1,1,30) do (
  curl.exe -fsS --max-time 1 http://127.0.0.1:8765/health >nul 2>&1 && goto open_dashboard
  timeout /t 1 /nobreak >nul
)

echo.
echo Remote Agent belum menjawab pada port tunnel 8765.
echo Pastikan ASUS menyala, agent aktif, dan login SSH berhasil.
pause
exit /b 2

:open_dashboard
echo Koneksi tunnel siap. Membuka TCS Control Center...
start "" "http://127.0.0.1:8765/"
exit /b 0
