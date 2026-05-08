param(
    [ValidateSet('Build', 'Rebuild')]
    [string]$Target = 'Build',

    [string]$Configuration = 'Debug',

    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'

$vswhere = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Microsoft Visual Studio\Installer\vswhere.exe'
if (!(Test-Path $vswhere)) {
    throw 'vswhere.exe not found. Install Visual Studio 2022 or Build Tools.'
}

$msbuildPath = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuildPath) {
    $msbuildPath = & $vswhere -latest -products Microsoft.VisualStudio.Product.BuildTools -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}

if (-not $msbuildPath) {
    throw 'MSBuild.exe not found. Install Visual Studio 2022 or Build Tools with MSBuild.'
}

Write-Host "MSBuild Path: $msbuildPath"

& $msbuildPath 'PEBakery.sln' /restore /m /t:$Target /p:Configuration=$Configuration /p:Platform=$Platform /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary
