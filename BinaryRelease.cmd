@echo off
SETLOCAL
TITLE Pack Release Binary of PEBakery

REM Get Directory Pathes
SET BaseDir=%~dp0
IF %BaseDir:~-1%==\ SET BaseDir=%BaseDir:~0,-1%
REM SET BaseDir=%BaseDir%\..\..
SET DestDir=%~dp0\BinaryRelease

REM Nuget Package
res\nuget restore

REM Build PEBakery solution
REM Adjust these statements according to your envrionment
REM SET MSBUILD_PATH="%WinDir%\Microsoft.NET\Framework\v4.0.30319\"
SET MSBUILD_PATH="%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Community\MSBuild\15.0\bin"
%MSBUILD_PATH%\MSBuild.exe %BaseDir%\LauncherNative /p:Configuration=Release /property:Platform=Win32 /target:Rebuild
%MSBUILD_PATH%\MSBuild.exe %BaseDir% /p:Configuration=Release /property:Platform="Any CPU" /target:Rebuild

REM Copy Files
RD /S /Q %DestDir%
MKDIR %DestDir%
COPY %BaseDir%\LauncherNative\Release\PEBakeryLauncher.exe %DestDir%\PEBakeryLauncher.exe
XCOPY /S /E /C /I %BaseDir%\PEBakery\bin\Release %DestDir%\Binary
DEL %DestDir%\Binary\*.pdb
DEL %DestDir%\Binary\*.xml
DEL %DestDir%\Binary\*.config
DEL %DestDir%\Binary\x64\*.so
RD /S /Q %DestDir%\Binary\armhf
RD /S /Q %DestDir%\Binary\arm64
IF EXIST %DestDir%\Binary\*.db DEL %DestDir%\Binary\*.db
IF EXIST %DestDir%\Binary\Database RD /S /Q %DestDir%\Binary\Database
IF EXIST %DestDir%\Database RD /S /Q %DestDir%\Database
COPY %BaseDir%\LICENSE %DestDir%\Binary
COPY %BaseDir%\LICENSE.GPLv3 %DestDir%\Binary
ENDLOCAL
