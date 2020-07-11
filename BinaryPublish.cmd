@echo off
SETLOCAL
TITLE Pack Release Binary of PEBakery
ECHO Pack Release Binary of PEBakery

REM Find MSBuild Binary
SET VSWHERE="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
FOR /F "tokens=* usebackq" %%a in (`%VSWHERE% -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) DO SET MSBUILD=%%a
ECHO Path of MSBUILD = %MSBUILD%
ECHO.

REM Get Directory Pathes
SET BaseDir=%~dp0
IF %BaseDir:~-1%==\ SET BaseDir=%BaseDir:~0,-1%
SET SrcDir=%BaseDir%\PEBakery\bin\Release\netcoreapp3.1
SET PublishDir=%~dp0\Publish
SET SevenZipExe=%PublishDir%\_tools\7za_x64.exe
SET UpxExe=%PublishDir%\_tools\upx_x64.exe
SET PublishMode=0
:_BUILD
IF %PublishMode%==0 (
    SET PublishName=PEBakery-nightly-fxdep
)
IF %PublishMode%==1 (
    SET PublishName=PEBakery-nightly-sc
)
SET DestDir=%PublishDir%\%PublishName%
SET DestBinDir=%DestDir%\Binary

REM Build PEBakery Binaries
IF EXIST "%PublishDir%\%PublishName%.7z" DEL "%PublishDir%\%PublishName%.7z"
IF EXIST "%DestDir%" RD /S /Q "%DestDir%"
MKDIR %DestDir%
MKDIR %DestBinDir%
PUSHD %BaseDir%
dotnet clean -c Release -verbosity:minimal
"%MSBUILD%" -target:Rebuild -verbosity:minimal "%BaseDir%\LauncherNative" /p:Configuration=Release /property:Platform=Win32
IF %PublishMode%==0 (
    dotnet publish -c Release -r win-x64 --force --self-contained=false -o "%DestBinDir%" PEBakery
)
IF %PublishMode%==1 (
    dotnet publish -c Release -r win-x64 --force --self-contained=true /p:PublishTrimmed=true /p:PublishSingleFile=false -o "%DestBinDir%" PEBakery
)
POPD

REM Copy Additional Files
COPY "%BaseDir%\LauncherNative\Release\PEBakeryLauncher.exe" "%DestDir%\PEBakeryLauncher.exe"
REM "%UpxExe%" "%DestDir%\PEBakeryLauncher.exe"
COPY "%BaseDir%\LICENSE" "%DestBinDir%"
COPY "%BaseDir%\LICENSE.GPLv3" "%DestBinDir%"

REM Filter Runtime Files
IF %PublishMode%==0 (
    IF EXIST "%DestBinDir%\runtimes\win-x64\native\7z.dll" COPY "%DestBinDir%\runtimes\win-x64\native\7z.dll" "%DestBinDir%\7z.dll"
    IF EXIST "%DestBinDir%\runtimes" RD /S /Q "%DestBinDir%\runtimes"
)

REM Delete Unnecessary Files
IF EXIST "%DestBinDir%\*.pdb" DEL "%DestBinDir%\*.pdb"
IF EXIST "%DestBinDir%\*.xml" DEL "%DestBinDir%\*.xml"
IF EXIST "%DestBinDir%\*.db" DEL "%DestBinDir%\*.db"
IF EXIST "%DestBinDir%\Database" DEL "%DestBinDir%\Database"
IF EXIST "%DestDir%\Database" DEL "%DestDir%\Database"

REM Create Release Binary
PUSHD "%PublishDir%"
"%SevenZipExe%" a %PublishName%.7z .\%PublishName%\*
POPD

IF %PublishMode%==0 (
    SET PublishMode=1
    GOTO _BUILD
)

ENDLOCAL
