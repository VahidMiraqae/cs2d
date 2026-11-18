# Builds the React client + ASP.NET Core server and hosts the game on the local network.
# Usage:  .\publish.ps1            (build + run)     .\publish.ps1 -NoRun   (build only)     .\publish.ps1 -Port 8080
param([int]$Port = 5275, [switch]$NoRun)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out = Join-Path $root 'publish'

Write-Host '==> Building client' -ForegroundColor Cyan
Push-Location (Join-Path $root 'client')
if (-not (Test-Path node_modules)) { npm install; if ($LASTEXITCODE) { throw 'npm install failed' } }
npm run build; if ($LASTEXITCODE) { throw 'client build failed' }
Pop-Location

Write-Host '==> Copying client into server/wwwroot' -ForegroundColor Cyan
$www = Join-Path $root 'server\wwwroot'
if (Test-Path $www) { Remove-Item $www -Recurse -Force }
Copy-Item (Join-Path $root 'client\dist') $www -Recurse

Write-Host '==> Publishing server' -ForegroundColor Cyan
dotnet publish (Join-Path $root 'server') -c Release -o $out; if ($LASTEXITCODE) { throw 'dotnet publish failed' }
Write-Host "Published to $out" -ForegroundColor Green
if ($NoRun) { return }

# Allow LAN connections through Windows Firewall (needs admin; skipped with a hint otherwise)
$rule = "CS2D $Port"
if (-not (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)) {
  try { New-NetFirewallRule -DisplayName $rule -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -Profile Private,Domain | Out-Null; Write-Host "Firewall rule '$rule' added" -ForegroundColor Green }
  catch { Write-Host "Could not add a firewall rule (run PowerShell as Administrator once if others can't connect)." -ForegroundColor Yellow }
}

Write-Host "`nCS2D is hosted. Others on your network can open:" -ForegroundColor Green
Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notmatch '^(127|169\.254)\.' -and $_.PrefixOrigin -ne 'WellKnown' } |
  ForEach-Object { Write-Host "  http://$($_.IPAddress):$Port" -ForegroundColor White }
Write-Host "  (this PC: http://localhost:$Port)   Ctrl+C to stop`n"

$env:urls = "http://0.0.0.0:$Port"
Set-Location $out
dotnet .\Cs2D.Server.dll
