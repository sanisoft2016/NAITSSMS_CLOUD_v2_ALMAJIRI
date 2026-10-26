# Opens localhost:15432 -> the live ASMS Postgres (postgres-service-naitsasms) on the VPS, for
# the API's "https-remote-asms-db" launch profile / NAITS_USE_REMOTE_ASMS_DB=true.
# Requires the "silkcoat-vps" host alias in ~/.ssh/config. Leave this window open; Ctrl+C closes it.
#
#   powershell -ExecutionPolicy Bypass -File NaitsSmsApi/scripts/remote-asms-db-tunnel.ps1
#
# WARNING: this is the production database. Anything the local API writes is live for the school.

param(
    [string]$HostAlias = 'silkcoat-vps',
    [int]$LocalPort = 15432,
    [string]$Service = 'postgres-service-naitsasms'
)

$ErrorActionPreference = 'Stop'

if (Get-NetTCPConnection -LocalPort $LocalPort -State Listen -ErrorAction SilentlyContinue) {
    Write-Host "Port $LocalPort is already listening - a tunnel is probably open already." -ForegroundColor Yellow
    exit 0
}

# The ClusterIP changes if the Service is ever recreated, so look it up each time.
$target = ssh -o BatchMode=yes $HostAlias "kubectl -n default get svc $Service -o jsonpath='{.spec.clusterIP}:{.spec.ports[0].port}'"
if ($LASTEXITCODE -ne 0 -or $target -notmatch '^\d+\.\d+\.\d+\.\d+:\d+$') {
    Write-Host "Could not resolve $Service on $HostAlias (got '$target')." -ForegroundColor Red
    exit 1
}

# Reconnect after network drops or sleep; Ctrl+C stops the loop.
while ($true) {
    Write-Host "$(Get-Date -Format 'HH:mm:ss') Tunnel: localhost:$LocalPort -> $target on $HostAlias (live ASMS database)" -ForegroundColor Cyan
    ssh -N -o BatchMode=yes -o ExitOnForwardFailure=yes -o ServerAliveInterval=30 -o ServerAliveCountMax=3 `
        -L "127.0.0.1:${LocalPort}:${target}" $HostAlias
    Write-Host "$(Get-Date -Format 'HH:mm:ss') Tunnel dropped (exit $LASTEXITCODE); reconnecting in 5 seconds..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5
}
