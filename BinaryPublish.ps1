# Script Parameters
# .\BinaryPublish.ps1 -nightly -noclean
param (
    [switch]$nightly = $false,
    [switch]$noclean = $false
)

# CI Mode?
if ($nightly) {
    $BinaryName = "nightly"
} else {
    $BinaryName = "release"
}

# Banner
Write-Host "[*] Publishing PEBakery ${BinaryName} binaries..." -ForegroundColor Yellow

# Find MSBuild location
$VSWhere = "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$MSBuild = & "$VSWhere" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe
Write-Output "MSBuild Path = ${MSBuild}"

# Get directory paths
$BaseDir = $PSScriptRoot
$PublishDir = "${BaseDir}\Publish"
$ToolDir = "${PublishDir}\_tools"
$SevenZipExe = "${ToolDir}\7za_x64.exe"
# $UpxExe = "${ToolDir}\upx_x64.exe"

$Cores = ${Env:NUMBER_OF_PROCESSORS}

# Clean the solution and restore NuGet packages
if ($noclean -eq $false) {
    Push-Location "${BaseDir}"
    Write-Output ""
    Write-Host "[*] Cleaning the solution" -ForegroundColor Yellow
    dotnet clean -c Release -verbosity:minimal
    Write-Output ""
    Write-Host "[*] Restore NuGet packages" -ForegroundColor Yellow
    dotnet restore --force PEBakery
    Pop-Location
}

# Loop tp publish PEBakery
enum PublishModes
{
    # Runtime-dependent cross-platform binary
    RuntimeDependent = 2
    # Self-contained x64
    SelfContained = 3
}
foreach ($PublishMode in [PublishModes].GetEnumValues())
{
    Write-Output ""
    Write-Host "[*] Publish ${PublishMode} ${BinaryName} PEBakery" -ForegroundColor Yellow
    $PublishModeInt = ${PublishMode}.value__
    if ($PublishMode -eq [PublishModes]::RuntimeDependent) {
        $PublishName = "PEBakery-${BinaryName}-rt"
    } elseif ($PublishMode -eq [PublishModes]::SelfContained) {
        $PublishName = "PEBakery-${BinaryName}-sc"
    } else {
        Write-Host "Invalid publish mode ${PublishMode} (${PublishModeInt})" -ForegroundColor Red
        exit 1
    }

    $DestDir = "${PublishDir}\${PublishName}"
    $DestBinDir = "${DestDir}\Binary"

    # Remove old publish files
    Remove-Item "${DestDir}" -Recurse -ErrorAction SilentlyContinue
    Remove-Item "${PublishDir}\${PublishName}.7z" -ErrorAction SilentlyContinue

    New-Item "${DestDir}" -ItemType Directory -ErrorAction SilentlyContinue
    New-Item "${DestBinDir}" -ItemType Directory -ErrorAction SilentlyContinue
    
    # Build and copy PEBakeryLauncher
    Push-Location "${BaseDir}"
    Write-Output ""
    Write-Host "[*] Build PEBakeryLauncher" -ForegroundColor Yellow
    & "${MSBuild}" -target:Rebuild -verbosity:minimal Launcher /p:Configuration=Release /p:Platform=Win32 /p:PublishMacro="PUBLISH_MODE=${PublishModeInt}"
    Copy-Item "${BaseDir}\Launcher\Win32\Release\PEBakeryLauncher.exe" -Destination "${DestDir}\PEBakeryLauncher.exe"
    # & "${UpxExe}" "${DestDir}\PEBakeryLauncher.exe"

    # Publish PEBakery
    Write-Output ""
    Write-Host "[*] Build PEBakery" -ForegroundColor Yellow
    if ($PublishMode -eq [PublishModes]::RuntimeDependent) {
        dotnet publish -c Release -o "${DestBinDir}" PEBakery
    } elseif ($PublishMode -eq [PublishModes]::SelfContained) {
        # dotnet publish -c Release -r win-x64 --self-contained=true /p:PublishTrimmed=true -o "${DestBinDir}" PEBakery
        # PEBakery crashes if a PublishTrimmed=true is set
        # Unhandled exception.
        #   Cannot print exception string because Exception.ToString() failed.
        dotnet publish -c Release -r win-x64 --self-contained -o "${DestBinDir}" PEBakery
    }
    Pop-Location

    # Handle native bnaries
    if ($PublishMode -eq [PublishModes]::RuntimeDependent) {
        # PEBakery does not support armhf
        Remove-Item "${DestBinDir}\runtimes\win-arm" -Recurse
    } elseif ($PublishMode -eq [PublishModes]::SelfContained) {
        # Flatten the location of 7z.dll
        Copy-Item "${DestBinDir}\runtimes\win-x64\native\*" -Destination "${DestBinDir}"
        Remove-Item "${DestBinDir}\runtimes" -Recurse
    }

    # Delete unnecessary files
    Remove-Item "${DestBinDir}\*.pdb" -ErrorAction SilentlyContinue
    Remove-Item "${DestBinDir}\*.xml" -ErrorAction SilentlyContinue
    Remove-Item "${DestBinDir}\*.db" -ErrorAction SilentlyContinue
    Remove-Item "${DestBinDir}\Database" -Recurse -ErrorAction SilentlyContinue
    Remove-Item "${DestDir}\Database" -Recurse -ErrorAction SilentlyContinue
    Remove-Item "${DestBinDir}\magic.mgc"  -ErrorAction SilentlyContinue

    # Copy license files
    Copy-Item "${BaseDir}\LICENSE" "${DestBinDir}"
    Copy-Item "${BaseDir}\LICENSE.GPLv3" "${DestBinDir}"

    # Create release 7z archive
    Write-Output ""
    Write-Host "[*] Create ${PublishMode} ${BinaryName} archive" -ForegroundColor Yellow
    Push-Location "${PublishDir}"
    & "${SevenZipExe}" a "-mmt=${Cores}" "${PublishName}.7z" ".\${PublishName}\*"
    Pop-Location
}
