# Restores, builds and runs every automated test, then (optionally) starts the app.
#   .\build-and-test.ps1          build + unit tests
#   .\build-and-test.ps1 -Run     build + unit tests, then start the app on http://localhost:5080
param([switch]$Run)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "== Restore ==" -ForegroundColor Cyan
dotnet restore UncoveringGreatnessCRM.sln
Write-Host "== Build ==" -ForegroundColor Cyan
dotnet build UncoveringGreatnessCRM.sln --no-restore -c Debug
Write-Host "== Unit tests ==" -ForegroundColor Cyan
dotnet test tests/UnitTests/UncoveringGreatnessCRM.Tests.csproj --no-build -c Debug

if ($Run) {
    Write-Host "== Starting the app (Ctrl+C to stop) ==" -ForegroundColor Cyan
    dotnet run --no-build --project UncoveringGreatnessCRM.csproj
}
