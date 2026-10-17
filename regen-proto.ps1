<#
.SYNOPSIS
    Regenerates src/Network/Protobuf/Generated/Envelope.cs from the .proto files.

.DESCRIPTION
    The fast dev build (dev-server.ps1, ci=true) and container builds use the committed
    Envelope.cs instead of running protoc. Run this after editing a .proto; the Godot client
    links the same generated file, so both sides pick up the change. A running
    dev-server.ps1 notices the new file and restarts on its own.
#>
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src/Network/MUnique.OpenMU.Network.csproj'
dotnet build $project -p:ci=false -p:RunAnalyzers=false -v q
if ($LASTEXITCODE -ne 0) { throw "protoc build failed ($LASTEXITCODE)" }
Write-Host 'Envelope.cs regenerated.'
