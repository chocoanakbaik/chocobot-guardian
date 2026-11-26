# TCS Remote Agent repair
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$root = Join-Path $env:USERPROFILE 'TCS\ChocoAI'
$agentDir = Join-Path $root 'agent'
$webDir = Join-Path $root 'control-center'
$configDir = Join-Path $root 'config'
$taskName = 'TCS Choco AI Remote Agent'
$branchRaw = 'https://raw.githubusercontent.com/chocoanakbaik/chocobot-guardian/feature/tcs-control-center'
$stage = Join-Path $env:TEMP 'TCS-ControlCenter-Repair'
New-Item -ItemType Directory -Force -Path $stage, $agentDir, $webDir, $configDir | Out-Null

$files = @{
  'TCSRemoteAgent.cs' = "$branchRaw/agent/TCSRemoteAgent.cs"
  'index.html' = "$branchRaw/control-center/index.html"
  'styles.css' = "$branchRaw/control-center/styles.css"
  'app.js' = "$branchRaw/control-center/app.js"
}
foreach ($name in $files.Keys) {
  Invoke-WebRequest -UseBasicParsing -Uri $files[$name] -OutFile (Join-Path $stage $name)
}

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
  $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $csc)) { throw 'CSHARP_COMPILER_NOT_FOUND' }

$newExe = Join-Path $stage 'TCSRemoteAgent.exe'
& $csc /nologo /optimize+ /target:winexe /platform:anycpu /r:System.Web.Extensions.dll /out:$newExe (Join-Path $stage 'TCSRemoteAgent.cs')
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $newExe)) { throw 'AGENT_COMPILE_FAILED' }

$taskCmd = Join-Path $env:WINDIR 'System32\schtasks.exe'
& $taskCmd /End /TN $taskName 2>$null | Out-Null
Get-Process -Name TCSRemoteAgent -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

$exePath = Join-Path $agentDir 'TCSRemoteAgent.exe'
$backupPath = Join-Path $agentDir 'TCSRemoteAgent.previous.exe'
if (Test-Path -LiteralPath $backupPath) { Remove-Item -LiteralPath $backupPath -Force }
if (Test-Path -LiteralPath $exePath) { Move-Item -LiteralPath $exePath -Destination $backupPath -Force }
Move-Item -LiteralPath $newExe -Destination $exePath -Force

Copy-Item -LiteralPath (Join-Path $stage 'TCSRemoteAgent.cs') -Destination (Join-Path $agentDir 'TCSRemoteAgent.cs') -Force
foreach ($name in @('index.html','styles.css','app.js')) {
  Copy-Item -LiteralPath (Join-Path $stage $name) -Destination (Join-Path $webDir $name) -Force
}

& $taskCmd /Run /TN $taskName | Out-Null
Start-Sleep -Seconds 3

try {
  $page = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:8765/index.html' -TimeoutSec 6
  $title = [regex]::Match($page.Content, '<title>(.*?)</title>').Groups[1].Value
  if ($page.StatusCode -ne 200 -or $title -ne 'TCS Control Center') { throw 'Unexpected dashboard response.' }
  Write-Output 'REPAIR=SUCCESS'
  Write-Output 'DASHBOARD_HTTP=200'
  Write-Output 'DASHBOARD_TITLE=TCS Control Center'
  Write-Output 'TOKEN=UNCHANGED'
} catch {
  Write-Output ('REPAIR=COMPILED_BUT_DASHBOARD_TEST_FAILED: ' + $_.Exception.Message)
  Write-Output 'Check TCS Remote Agent task and local agent log. Token was not changed.'
}
