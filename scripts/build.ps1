param([switch]$Publish, [switch]$Test)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { 'dotnet' }
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
if ($Test) {
    & $dotnet run --project (Join-Path $projectRoot 'tests\Bohairic.Tests\Bohairic.Tests.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
$project = Join-Path $projectRoot 'src\Bohairic.Desktop\Bohairic.Desktop.csproj'
if ($Publish) {
    & $dotnet publish $project -c Release -o (Join-Path $projectRoot 'artifacts\BohairicDictionary')
} else {
    & $dotnet build $project -c Release
}
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
