param([switch]$SkipRestore, [switch]$ManagedOnly, [string]$NativeCompiler = $env:MIR4_NATIVE_COMPILER)
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
if (-not $ManagedOnly) {
    if (-not $NativeCompiler) { throw 'Set MIR4_NATIVE_COMPILER or pass -NativeCompiler pointing to zig.exe. Use -ManagedOnly for transport/cipher source tests without a runnable native dependency.' }
    & (Join-Path $projectRoot 'native/build.ps1') -Compiler $NativeCompiler
    if (-not (Test-Path (Join-Path $projectRoot 'bin/Debug/Mir4Encryption.dll'))) { throw 'Native DLL was not produced.' }
}
& dotnet run --project (Join-Path $projectRoot 'tests/TransportRegression') --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Transport regression tests failed.' }
& dotnet run --project (Join-Path $projectRoot 'tests/CaptureRegression') --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Capture regression tests failed.' }
if ($ManagedOnly) {
    & dotnet run --project (Join-Path $projectRoot 'tests/CipherRegression') --verbosity quiet -- --managed-only
} else {
    & dotnet run --project (Join-Path $projectRoot 'tests/CipherRegression') --verbosity quiet
}
if ($LASTEXITCODE -ne 0) { throw 'Cipher regression tests failed.' }
