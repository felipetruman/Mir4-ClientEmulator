param([Parameter(Mandatory=$true)][string]$Compiler)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path (Join-Path $PSScriptRoot 'current_parameters.h'))) {
    throw 'Generate current_parameters.h from a verified installed client before building.'
}
if (-not (Test-Path (Join-Path $PSScriptRoot 'current-profile.json'))) { throw 'A verified current-profile.json is required.' }
$outputDirectory = Join-Path $projectRoot 'bin/Debug'
New-Item -ItemType Directory -Force $outputDirectory | Out-Null
& $Compiler cc -target x86_64-windows-gnu -shared -O2 -Wall -Wextra (Join-Path $PSScriptRoot 'encryption.c') -o (Join-Path $outputDirectory 'Mir4Encryption.dll') -lkernel32
if ($LASTEXITCODE -ne 0) { throw 'Native cipher build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'current-profile.json') -Destination $outputDirectory -Force
$releaseDirectory = Join-Path $projectRoot 'bin/Release'
New-Item -ItemType Directory -Force $releaseDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $outputDirectory 'Mir4Encryption.dll') -Destination $releaseDirectory -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'current-profile.json') -Destination $releaseDirectory -Force
