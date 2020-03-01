@echo off
SETLOCAL
TITLE Pack Release Binary of PEBakery
ECHO Pack Release Binary of PEBakery

REM Get Directory Pathes
SET BaseDir=%~dp0
IF %BaseDir:~-1%==\ SET BaseDir=%BaseDir:~0,-1%
SET SrcDir=%BaseDir%\PEBakery\bin\Release\netcoreapp3.1
SET DestDir=%~dp0\BinaryRelease

REM Build PEBakery solution
SET VSWHERE="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
FOR /F "tokens=* usebackq" %%a in (`%VSWHERE% -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) DO SET MSBUILD="%%a"
ECHO Path of MSBUILD = %MSBUILD%
ECHO.

REM Build PEBakery Binaries
%MSBUILD% -t:restore %BaseDir%\LauncherNative /p:Configuration=Release /property:Platform=Win32 /target:Rebuild
dotnet clean -c Release 
dotnet build -c Release --force PEBakery 

REM Copy Files
RD /S /Q %DestDir%
MKDIR %DestDir%
COPY %BaseDir%\LauncherNative\Release\PEBakeryLauncher.exe %DestDir%\PEBakeryLauncher.exe
XCOPY /S /E /C /I %SrcDir% %DestDir%\Binary
COPY %BaseDir%\LICENSE %DestDir%\Binary
COPY %BaseDir%\LICENSE.GPLv3 %DestDir%\Binary

REM Filter Runtime Files
IF EXIST %DestDir%\Binary\runtimes RD /S /Q %DestDir%\Binary\runtimes
XCOPY /S /E /C /I %SrcDir%\runtimes\win-x86 %DestDir%\Binary\runtimes\win-x86
XCOPY /S /E /C /I %SrcDir%\runtimes\win-x64 %DestDir%\Binary\runtimes\win-x64

REM Delete Unnecessary Files
IF EXIST %DestDir%\Binary\*.pdb DEL %DestDir%\Binary\*.pdb
IF EXIST %DestDir%\Binary\*.xml DEL %DestDir%\Binary\*.xml
IF EXIST %DestDir%\Binary\x86\*.so DEL %DestDir%\Binary\x86\*.so
IF EXIST %DestDir%\Binary\x64\*.so DEL %DestDir%\Binary\x64\*.so
IF EXIST %DestDir%\Binary\x64\*.dylib DEL %DestDir%\Binary\x64\*.dylib
IF EXIST %DestDir%\Binary\armhf RD /S /Q %DestDir%\Binary\armhf
IF EXIST %DestDir%\Binary\arm64 RD /S /Q %DestDir%\Binary\arm64
IF EXIST %DestDir%\Binary\*.db DEL %DestDir%\Binary\*.db
IF EXIST %DestDir%\Binary\Database RD /S /Q %DestDir%\Binary\Database
IF EXIST %DestDir%\Database RD /S /Q %DestDir%\Database

ENDLOCAL
