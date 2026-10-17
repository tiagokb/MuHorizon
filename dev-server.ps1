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

# PostgreSQL needs Docker; start Docker Desktop when it isn't running (e.g. right after boot).
docker version --format '{{.Server.Version}}' 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    $dockerDesktop = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
    if (-not (Test-Path $dockerDesktop)) { throw 'Docker Desktop not found; it is needed for PostgreSQL.' }
    Write-Host 'Starting Docker Desktop...'
    Start-Process $dockerDesktop
    do {
        Start-Sleep -Seconds 3
        docker version --format '{{.Server.Version}}' 2>$null | Out-Null
    } while ($LASTEXITCODE -ne 0)
}

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

# The console output (build errors, hot reload results, restarts) is also kept in logs/watch.log,
# so it can be followed from another terminal or tool while this window runs the server.
New-Item -ItemType Directory -Force logs | Out-Null
$watchLog = Join-Path (Get-Location) 'logs/watch.log'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$command = if ($NoWatch) { 'dotnet run --no-launch-profile' } else { 'dotnet watch --non-interactive run --no-launch-profile' }

# cmd merges stderr into stdout before PowerShell sees it: in Windows PowerShell 5.1 a native
# command's stderr piped through 2>&1 turns into error records, which stop this script.
# dotnet watch itself sometimes crashes when files are added while it reloads the projects
# ("Cannot find the old value"), so it's restarted until this window is closed (or Ctrl+C).
Clear-Content $watchLog -ErrorAction SilentlyContinue
while ($true) {
    cmd /c "chcp 65001 >nul & $command 2>&1" | Tee-Object -FilePath $watchLog -Append
    if ($NoWatch) { break }
    Write-Host 'dotnet watch stopped; restarting in 3 s (close the window or Ctrl+C to quit)...'
    Start-Sleep -Seconds 3
}
