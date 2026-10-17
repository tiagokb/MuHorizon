<#
.SYNOPSIS
    Runs the OpenMU server natively with hot reload, for day-to-day development.

.DESCRIPTION
    Only PostgreSQL runs in Docker. The server runs on the host under `dotnet watch`, so most
    code edits are applied in place (hot reload) instead of rebuilding a container (~5 min).
    Edits hot reload can't apply (new types, signature changes...) restart the process
    automatically.

    Build profile used here (fast, dev only):
      ci=true            skips the code generators that run on every build (packet docs via npx,
                         persistence model generator, sass) and uses their committed outputs.
                         It also skips protoc: after editing a .proto run ./regen-proto.ps1.
      RunAnalyzers=false skips StyleCop; CI and normal builds still run it.

.PARAMETER NoWatch
    Runs once with `dotnet run` instead of `dotnet watch`.

.EXAMPLE
    ./dev-server.ps1
#>
param([switch]$NoWatch)

$ErrorActionPreference = 'Stop'
$compose = Join-Path $PSScriptRoot 'deploy/all-in-one/docker-compose.yml'

# The containerized server would hold the same ports.
docker compose -f $compose stop openmu-dev | Out-Null
docker compose -f $compose up -d database | Out-Null
Write-Host 'Waiting for PostgreSQL...'
while (-not (docker exec database pg_isready -U postgres 2>$null)) { Start-Sleep -Seconds 1 }

$env:ASPNETCORE_ENVIRONMENT = 'Development'   # appsettings.Development.json: WebSocket on localhost (no admin rights needed)
$env:ASPNETCORE_URLS = 'http://localhost:8081' # admin panel, same port the container exposed
$env:ci = 'true'
$env:RunAnalyzers = 'false'
$env:DOTNET_WATCH_RESTART_ON_RUDE_EDIT = 'true'

Set-Location (Join-Path $PSScriptRoot 'src/Startup')
if ($NoWatch) {
    dotnet run --no-launch-profile
}
else {
    dotnet watch --non-interactive run --no-launch-profile
}
