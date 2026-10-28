<#
.SYNOPSIS
    Builds a release locally: self-contained publish, then a Velopack installer and update package.
.DESCRIPTION
    Output lands in artifacts/releases: Halyard-<channel>-Setup.exe (what users download), the
    .nupkg packages and the release feed that installed copies read to update themselves.
    CI runs the same script, then uploads artifacts/releases to GitHub Releases.
.EXAMPLE
    ./tools/Publish-Release.ps1 -Version 0.1.0-beta.1 -Channel beta
#>
param(
    [Parameter(Mandatory)][string]$Version,
    [ValidateSet('stable', 'beta')][string]$Channel = 'beta',
    [string]$RepositoryUrl,
    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = 'Stop'
$publish = Join-Path $Root 'artifacts\publish'
$releases = Join-Path $Root 'artifacts\releases'
Remove-Item -Recurse -Force $publish -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $releases | Out-Null

$publishArgs = @(
    'publish', (Join-Path $Root 'src\Halyard\Halyard.csproj'),
    '-c', 'Release', '-r', 'win-x64', '--self-contained',
    '-o', $publish, "-p:Version=$Version", '-nologo')
if ($RepositoryUrl) { $publishArgs += "-p:RepositoryUrl=$RepositoryUrl" }

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    dotnet tool install -g vpk --version 1.2.161
    $env:PATH += ";$env:USERPROFILE\.dotnet\tools"
}

# Stable builds use Velopack's default channel; beta builds get their own feed.
$packArgs = @(
    'pack', '--packId', 'Halyard', '--packVersion', $Version, '--packTitle', 'Halyard',
    '--packDir', $publish, '--mainExe', 'Halyard.exe', '--outputDir', $releases,
    '--icon', (Join-Path $Root 'src\Halyard\Assets\AppIcon.ico'))
if ($Channel -eq 'beta') { $packArgs += @('--channel', 'beta') }

vpk @packArgs
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed ($LASTEXITCODE)" }

Get-ChildItem $releases | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
