# Script Parameters
param (
    [switch]$noclean = $false
)

Write-Host "[Publishing PEBakery]" -ForegroundColor Yellow

# Find MSBuild location
$VSWhere = "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$MSBuild = & "$VSWhere" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe
Write-Output "MSBuild Path = ${MSBuild}"

# Get directory paths
$BaseDir = $PSScriptRoot
$PublishDir = "${BaseDir}\Publish"
$ToolDir = "${PublishDir}\tools"
$SevenZipExe = "${ToolDir}\7za_x64.exe"
# $UpxExe = "${ToolDir}\upx_x64.exe"

# Clean the solution
if ($noclean -eq $false) {
    Push-Location "${BaseDir}"
    Write-Output ""
    Write-Host "[*] Cleaning the solution" -ForegroundColor Yellow
    dotnet clean -c Release -verbosity:minimal
    Pop-Location
}

# Build PEBakeryLauncher
Write-Output ""
Write-Host "[*] Build PEBakeryLauncher" -ForegroundColor Yellow
& "${MSBuild}" -target:Rebuild -verbosity:minimal "${BaseDir}\LauncherNative" /p:Configuration=Release /property:Platform=Win32

# Loop tp publish PEBakery
enum PublishModes
{
    FxDependent = 0
    SelfContained
}
foreach ($PublishMode in [PublishModes].GetEnumValues())
{
    Write-Output ""
    Write-Host "[*] Publish ${PublishMode} PEBakery build" -ForegroundColor Yellow
    if ($PublishMode -eq [PublishModes]::FxDependent) {
        $PublishName = "PEBakery-nightly-fxdep"
    } elseif ($PublishMode -eq [PublishModes]::SelfContained) {
        $PublishName = "PEBakery-nightly-sc"
    } else {
        Write-Output "Invalid PublishMode"
        exit 1
    }

    $DestDir = "${PublishDir}\${PublishName}"
    $DestBinDir = "${DestDir}\Binary"

    # Remove old publish files
    Remove-Item "${PublishDir}\${PublishName}.7z" -ErrorAction SilentlyContinue
    Remove-Item "${Dest}.7z" -Recurse -ErrorAction Ignore

    New-Item "${DestDir}" -ItemType Directory -ErrorAction SilentlyContinue
    New-Item "${DestBinDir}" -ItemType Directory -ErrorAction SilentlyContinue
    
    # Call dotnet command
    Push-Location "${BaseDir}"
    if ($PublishMode -eq [PublishModes]::FxDependent) {
        dotnet publish -c Release --force --self-contained=false -o "${DestBinDir}" PEBakery
    } elseif ($PublishMode -eq [PublishModes]::SelfContained) {
        dotnet publish -c Release -r win-x64 --force --self-contained=true /p:PublishTrimmed=true /p:PublishSingleFile=false -o "${DestBinDir}" PEBakery
    }
    Pop-Location

    # Handle Native Binaries
    if ($PublishMode -eq [PublishModes]::FxDependent) {
        Move-Item "${DestBinDir}\runtimes" -Destination "${DestBinDir}\runtimes_bak"
        New-Item "${DestBinDir}\runtimes" -ItemType Directory
        Copy-Item "${DestBinDir}\runtimes_bak\win*" -Destination "${DestBinDir}\runtimes" -Recurse
        Remove-Item "${DestBinDir}\runtimes_bak" -Recurse
    } elseif ($PublishMode -eq [PublishModes]::SelfContained) {
        # Flatten the location of 7z.dll
        Copy-Item "${DestBinDir}\runtimes\win-x64\native\7z.dll" -Destination "${DestBinDir}\7z.dll"
        Remove-Item "${DestBinDir}\runtimes" -Recurse
    }

    # Delete unnecessary files
    Remove-Item "${DestBinDir}\*.pdb" -ErrorAction SilentlyContinue
    Remove-Item "${DestBinDir}\*.xml" -ErrorAction SilentlyContinue
    Remove-Item "${DestBinDir}\*.db" -ErrorAction SilentlyContinue
    Remove-Item "${DestBinDir}\Database" -Recurse -ErrorAction SilentlyContinue
    Remove-Item "${DestDir}\Database" -Recurse -ErrorAction SilentlyContinue

    # Copy PEBakeryLauncher and license files
    Copy-Item "${BaseDir}\LauncherNative\Release\PEBakeryLauncher.exe" -Destination "${DestDir}\PEBakeryLauncher.exe"
    # & "${UpxExe}" "${DestDir}\PEBakeryLauncher.exe"
    Copy-Item "${BaseDir}\LICENSE" "${DestBinDir}"
    Copy-Item "${BaseDir}\LICENSE.GPLv3" "${DestBinDir}"

    # Create release 7z archive
    Write-Output ""
    Write-Host "[*] Create ${PublishMode} PEBakery release archive" -ForegroundColor Yellow
    Push-Location "${PublishDir}"
    & "${SevenZipExe}" a "${PublishName}.7z" ".\${PublishName}\*"
    Pop-Location
}
