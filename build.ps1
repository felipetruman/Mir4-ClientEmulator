param([switch]$SkipRestore)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
if (-not $SkipRestore) {
    $toolDir = Join-Path $projectRoot '.tools'
    New-Item -ItemType Directory -Force $toolDir | Out-Null
    $nugetPath = Join-Path $toolDir 'nuget.exe'
    if (-not (Test-Path -LiteralPath $nugetPath)) {
        Invoke-WebRequest 'https://dist.nuget.org/win-x86-commandline/v6.14.0/nuget.exe' -OutFile $nugetPath
    }
    & $nugetPath restore (Join-Path $projectRoot 'packages.config') -PackagesDirectory (Join-Path $projectRoot 'packages') -NonInteractive
    if ($LASTEXITCODE -ne 0) { throw 'Package restore failed.' }
}
& dotnet build (Join-Path $projectRoot 'Mir4ClientEmulator.csproj') --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
& dotnet run --project (Join-Path $projectRoot 'tests/TransportRegression') --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Transport regression tests failed.' }
