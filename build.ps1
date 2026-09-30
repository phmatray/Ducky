# Runs the Fallout build (build/_build.csproj, SPEC §19): ./build.ps1 [targets] [--parameters].
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
$env:DOTNET_NOLOGO = 1

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'dotnet not found: install the SDK pinned by global.json'
}
Write-Output "Microsoft (R) .NET SDK version $(dotnet --version)"

dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet fallout @args
exit $LASTEXITCODE
