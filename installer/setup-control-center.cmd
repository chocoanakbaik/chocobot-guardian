@echo off
setlocal EnableExtensions
title TCS Remote Agent Setup

set "ROOT=%USERPROFILE%\TCS\ChocoAI"
set "AGENT=%ROOT%\agent"
set "WEB=%ROOT%\control-center"
set "CONFIG=%ROOT%\config"
set "LOGS=%ROOT%\logs"
set "BRANCH_RAW=https://raw.githubusercontent.com/chocoanakbaik/chocobot-guardian/feature/tcs-control-center"

echo.
echo ============================================
echo   THE CHOCO STUDIOS - REMOTE AGENT SETUP
echo ============================================
echo.

if not exist "%AGENT%" mkdir "%AGENT%"
if not exist "%WEB%" mkdir "%WEB%"
if not exist "%CONFIG%" mkdir "%CONFIG%"
if not exist "%LOGS%" mkdir "%LOGS%"

echo [1/5] Mengunduh komponen TCS Control Center...
curl.exe -fL --retry 3 --retry-delay 3 -o "%AGENT%\TCSRemoteAgent.cs" "%BRANCH_RAW%/agent/TCSRemoteAgent.cs"
if errorlevel 1 goto download_failed
curl.exe -fL --retry 3 --retry-delay 3 -o "%WEB%\index.html" "%BRANCH_RAW%/control-center/index.html"
if errorlevel 1 goto download_failed
curl.exe -fL --retry 3 --retry-delay 3 -o "%WEB%\styles.css" "%BRANCH_RAW%/control-center/styles.css"
if errorlevel 1 goto download_failed
curl.exe -fL --retry 3 --retry-delay 3 -o "%WEB%\app.js" "%BRANCH_RAW%/control-center/app.js"
if errorlevel 1 goto download_failed

echo.
echo [2/5] Memeriksa compiler .NET Framework...
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Compiler C# bawaan .NET Framework tidak ditemukan.
  echo File sudah diunduh tetapi agent belum dikompilasi.
  echo Jangan tutup laptop. Laporkan pesan ini.
  exit /b 2
)

echo.
echo [3/5] Mengompilasi Remote Agent...
"%CSC%" /nologo /optimize+ /target:winexe /platform:anycpu /r:System.Web.Extensions.dll /out:"%AGENT%\TCSRemoteAgent.exe" "%AGENT%\TCSRemoteAgent.cs"
if errorlevel 1 (
  echo Kompilasi gagal. Log error di atas perlu diperiksa.
  exit /b 3
)

echo.
echo [4/5] Mendaftarkan agent agar berjalan saat akun Windows login...
schtasks /Create /SC ONLOGON /TN "TCS Choco AI Remote Agent" /TR "\"%AGENT%\TCSRemoteAgent.exe\"" /IT /F
if errorlevel 1 (
  echo Pendaftaran Task Scheduler gagal.
  echo Agent sudah dikompilasi, tetapi belum disetel auto-start.
  exit /b 4
)

echo.
echo Menjalankan agent sekarang...
schtasks /Run /TN "TCS Choco AI Remote Agent"
if errorlevel 1 (
  echo Agent dijadwalkan, tetapi tidak berhasil dimulai sekarang.
  echo Coba periksa status task kemudian.
)

echo.
echo [5/5] Menunggu token akses lokal dibuat...
for /L %%I in (1,1,12) do (
  if exist "%CONFIG%\agent.token" goto token_ready
  timeout /t 1 /nobreak >nul
)

echo Token belum muncul. Baca log di "%LOGS%\remote-agent.log".
exit /b 5

:token_ready
echo.
echo ============================================
echo   TOKEN AKSES LOKAL - SIMPAN SECARA RAHASIA
echo ============================================
type "%CONFIG%\agent.token"
echo.
echo.
echo Setup selesai. Agent hanya mendengarkan pada 127.0.0.1:8765.
echo Jangan masukkan token ke GitHub atau membagikannya kepada siapa pun.
echo.
exit /b 0

:download_failed
echo.
echo Pengunduhan komponen gagal. Tidak ada perubahan sistem auto-start.
exit /b 1
