# =============================================================================
# PEBakery Build/Publish Powershell Script
# =============================================================================
# Intended to run on both Powershell (.NET Framework) and Powershell Core (.NET).
#
# On CI, use:
#    .\BinaryPublish.ps1 -nightly -noclean
# On Release, use:
#    .\BinaryPublish.ps1

# -----------------------------------------------------------------------------
# Script parameters & banner
# -----------------------------------------------------------------------------
param (
    [switch]$nightly = $false,
    [switch]$noclean = $false
)

# Is CI Mode?
if ($nightly) {
    $BinaryName = "nightly"
} else {
    $BinaryName = "release"
}

# Banner
Write-Host "[*] Publishing PEBakery ${BinaryName} binaries..." -ForegroundColor Cyan

# -----------------------------------------------------------------------------
# Publish mode/arch (Available & Activated)
# -----------------------------------------------------------------------------
# Available publish modes
enum PublishModes
{
    # Runtime-dependent cross-platform binary
    RuntimeDependent = 0
    # Self-contained
    SelfContained
}
# Available publish architectures (.exe arch in runtime-dependent, runtimeId in self-contained)
enum PublishArches
{
    None = 0
    x86
    x64
    arm64
}

# Activated publish modes & arches
$runModes = @(
    ,@( [PublishModes]::RuntimeDependent, [PublishArches]::None )
    ,@( [PublishModes]::SelfContained, [PublishArches]::x64 )
    ,@( [PublishModes]::SelfContained, [PublishArches]::arm64 )
)

# -----------------------------------------------------------------------------
# Find MSBuild location
# -----------------------------------------------------------------------------
$VSWhere = "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"

# Try normal Visual Studio Community/Professional/Enterprise version.
$MSBuild = & "$VSWhere" -latest -requires "Microsoft.Component.MSBuild" -find "MSBuild\**\Bin\MSBuild.exe"

# Try Visual Studio Build Tools.
If ($null -eq $MSBuild || $MSBuild -eq "") { 
    # Microsoft Visual Studio Build Tools requires '-products' argument.
    $MSBuild = & "$VSWhere" -latest -products "Microsoft.VisualStudio.Product.BuildTools" -requires "Microsoft.Component.MSBuild" -find "MSBuild\**\Bin\MSBuild.exe"
}

# Try fixed VS2022 (x64) MSBuild path, as a last resort
If ($null -eq $MSBuild || $MSBuild -eq "") { 
    $TryPath = "${Env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    if (Test-Path -Path $TryPath) { 
        $MSBuild = $TryPath 
    }
}

# Try fixed VS2022 (x86) MSBuild path, as a last resort
If ($null -eq $MSBuild || $MSBuild -eq "") { 
    $TryPath = "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    if (Test-Path -Path $TryPath) { 
        $MSBuild = $TryPath 
    }
}

# MSBuild path final check
If ($null -eq $MSBuild || $MSBuild -eq "") {
    Write-Output "Failed to find MSBuild path. Unable to build the Launcher."
}

# Print found MSBuild path.
Write-Output "MSBuild Path = [${MSBuild}]"

# -----------------------------------------------------------------------------
# Get directory paths & enviroment infomation
# -----------------------------------------------------------------------------
$BaseDir = $PSScriptRoot
$PublishDir = "${BaseDir}\Publish"
$ToolDir = "${PublishDir}\_tools"
# Unfortunately, 7zip does not provide arm64 build of 7za.exe yet. (v21.07)
$SevenZipExe = "${ToolDir}\7za_x64.exe"
# Detect installed .NET runtime version (from NetDetectorCli.vcxproj)
$NetDetectorExe = "${ToolDir}\NetDetectorCli_x64.exe"
# UPX minimizes release size, but many antiviruses definitely hate it.
# $UpxExe = "${ToolDir}\upx_x64.exe"
$Cores = ${Env:NUMBER_OF_PROCESSORS}
Write-Output "Cores = ${Cores}"

# -----------------------------------------------------------------------------
# Clean the solution and restore NuGet packages (if -noclean is not set)
# -----------------------------------------------------------------------------
# https://learn.microsoft.com/en-us/dotnet/api/microsoft.build.framework.loggerverbosity?view=msbuild-17-netcore
if ($noclean -eq $false) {
    Push-Location "${BaseDir}"

    Write-Output ""
    Write-Host "[*] Cleaning the projects" -ForegroundColor Yellow
    # Clean C++ projects
    $VCXProjects = (Get-ChildItem -Recurse -Path . -Include *.vcxproj | where {$_.PSIsContainer -eq $false}).fullname 
    $VCXProjects | ForEach-Object { & "${MSBuild}" "${PSItem}" /t:Clean -verbosity:quiet}
    # Clean C# projects
    $CSProjects = (Get-ChildItem -Recurse -Path . -Include *.csproj | where {$_.PSIsContainer -eq $false}).fullname
    $CSProjects | ForEach-Object { dotnet clean -c Release "${PSItem}" -verbosity:minimal }

    Write-Output ""
    Write-Host "[*] Restore NuGet packages" -ForegroundColor Yellow
    dotnet restore --force PEBakery

    Pop-Location
}

# -----------------------------------------------------------------------------
# Query installed .NET version
# -----------------------------------------------------------------------------
$NetVerMajor = 6
Write-Output ""
Write-Host "[*] Query Installed .NET ${NetVerMajor} version" -ForegroundColor Yellow
$NetVerMinor = & "$NetDetectorExe" --req-major $NetVerMajor --res-minor --win-desktop
$NetVerPatch = & "$NetDetectorExe" --req-major $NetVerMajor --res-patch --win-desktop
Write-Output "PEBakeryLauncher will search for .NET ${NetVerMajor}.${NetVerMinor}.${NetVerPatch}."

# -----------------------------------------------------------------------------
# Iterate each activated PublishMode
# -----------------------------------------------------------------------------
foreach ($runMode in $runModes)
{
    $PublishMode = $runMode[0]
    $PublishArch = $runMode[1]

    Write-Output ""
    Write-Host "[*] Publish PEBakery (${BinaryName}, ${PublishMode}, ${PublishArch})" -ForegroundColor Cyan

    # -------------------------------------------------------------------------
    # Set up publish variables
    # -------------------------------------------------------------------------
    switch ($PublishMode)
    {
        RuntimeDependent
        { 
            $LauncherMode = 2 # BUILD_NETCORE_RT_DEPENDENT
            $PublishName = "PEBakery-${BinaryName}-rt"
            $isRuntimeDependent = $true
            if ($PublishArch -ne [PublishArches]::None) {
                Write-Host "Invalid publish arch [${PublishArch}]" -ForegroundColor Red
                exit 1
            }
            Break
        }
        SelfContained
        { 
            $LauncherMode = 3 # BUILD_NETCORE_SELF_CONTAINED
            $PublishName = "PEBakery-${BinaryName}-sc_${PublishArch}"
            $isRuntimeDependent = $false
            if ($PublishArch -eq [PublishArches]::None) {
                Write-Host "Invalid publish arch [${PublishArch}]" -ForegroundColor Red
                exit 1
            }
            $PublishRuntimeId = "win-${PublishArch}"
            Break
        }
        default
        {
            Write-Host "Invalid publish mode [${PublishMode}]" -ForegroundColor Red
            exit 1
        }
    }
    
    $DestDir = "${PublishDir}\${PublishName}"
    $DestBinDir = "${DestDir}\Binary"

    # -------------------------------------------------------------------------
    # Remove old publish files
    # -------------------------------------------------------------------------
    Remove-Item "${DestDir}" -Recurse -ErrorAction SilentlyContinue
    Remove-Item "${PublishDir}\${PublishName}.7z" -ErrorAction SilentlyContinue

    New-Item "${DestDir}" -ItemType Directory -ErrorAction SilentlyContinue
    New-Item "${DestBinDir}" -ItemType Directory -ErrorAction SilentlyContinue
    
    # -------------------------------------------------------------------------
    # Build and copy PEBakeryLauncher
    # -------------------------------------------------------------------------
    Push-Location "${BaseDir}"
    Write-Output ""
    Write-Host "[*] Build PEBakeryLauncher" -ForegroundColor Yellow
    & "${MSBuild}" -target:Rebuild -verbosity:minimal Launcher /p:Configuration=Release /p:Platform=Win32 `
        /p:PublishMacro="PUBLISH_MODE=${LauncherMode}" `
        /p:NetVerMajor="NETCORE_TARGET_VER_MAJOR=${NetVerMajor}" `
        /p:NetVerMinor="NETCORE_TARGET_VER_MINOR=${NetVerMinor}" `
        /p:NetVerPatch="NETCORE_TARGET_VER_PATCH=${NetVerPatch}"
    Copy-Item "${BaseDir}\Launcher\Win32\Release\PEBakeryLauncher.exe" -Destination "${DestDir}\PEBakeryLauncher.exe"
    Pop-Location

    # -------------------------------------------------------------------------
    # Publish PEBakery
    # -------------------------------------------------------------------------
    # WPF is not compatible with 'PublishTrimmed=true' argument.
    Push-Location "${BaseDir}"
    Write-Output ""
    Write-Host "[*] Build PEBakery" -ForegroundColor Yellow
    if ($isRuntimeDependent -eq $true) {
        dotnet publish -c Release -o "${DestBinDir}" PEBakery
    } else {
        dotnet publish -c Release -r "${PublishRuntimeId}" --self-contained -o "${DestBinDir}" PEBakery
    }
    Pop-Location

    # -------------------------------------------------------------------------
    # Handle native binaries
    # -------------------------------------------------------------------------
    if ($isRuntimeDependent -eq $true) {
        # PEBakery does not support win-arm, linux, and macOS.
        Remove-Item "${DestBinDir}\runtimes\linux*" -Recurse
        Remove-Item "${DestBinDir}\runtimes\alpine*" -Recurse
        Remove-Item "${DestBinDir}\runtimes\osx*" -Recurse
        Remove-Item "${DestBinDir}\runtimes\win-arm" -Recurse
    } else {
        # Flatten the location of native libraries
        Copy-Item "${DestBinDir}\runtimes\${PublishRuntimeId}\native\*" -Destination "${DestBinDir}"
        Remove-Item "${DestBinDir}\runtimes" -Recurse
    }

    # -------------------------------------------------------------------------
    # Delete unnecessary files
    # -------------------------------------------------------------------------
    Remove-Item "${DestBinDir}\*.pdb" -ErrorAction SilentlyContinue
    Remove-Item "${DestBinDir}\*.xml" -ErrorAction SilentlyContinue
    Remove-Item "${DestBinDir}\*.db" -ErrorAction SilentlyContinue
    Remove-Item "${DestBinDir}\Database" -Recurse -ErrorAction SilentlyContinue
    Remove-Item "${DestDir}\Database" -Recurse -ErrorAction SilentlyContinue
    Remove-Item "${DestBinDir}\magic.mgc" -ErrorAction SilentlyContinue

    # -------------------------------------------------------------------------
    # Copy LICENSE files
    # -------------------------------------------------------------------------
    Copy-Item "${BaseDir}\LICENSE" "${DestBinDir}"
    Copy-Item "${BaseDir}\LICENSE.GPLv3" "${DestBinDir}"

    # -------------------------------------------------------------------------
    # Create release 7z archive
    # -------------------------------------------------------------------------
    Write-Output ""
    Write-Host "[*] Create ${PublishMode} ${BinaryName} archive" -ForegroundColor Yellow
    Push-Location "${PublishDir}"
    & "${SevenZipExe}" a "-mmt=${Cores}" "${PublishName}.7z" ".\${PublishName}\*"
    Pop-Location
}
