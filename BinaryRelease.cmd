@echo off
SETLOCAL
TITLE Pack Release Binary of PEBakery

REM Get Directory Pathes
SET BaseDir=%~dp0
SET DestDir=%~dp0\BinaryRelease

REM Build PEBakery solution
REM   Adjust these statements according to your envrionment
REM SET MSBUILD_PATH="%windir%\Microsoft.NET\Framework\v4.0.30319\"
SET MSBUILD_PATH="%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Community\MSBuild\15.0\bin"
%MSBUILD_PATH%\MSBuild.exe %BaseDir% /p:Configuration=Release /target:Rebuild

REM Copy files
RD /S /Q %DestDir%
MKDIR %DestDir%
REM COPY %BaseDir%\LauncherSharp\bin\Release\PEBakeryLauncher.exe %DestDir%\PEBakeryLauncher.exe
COPY %BaseDir%\Release\PEBakeryLauncher.exe %DestDir%\PEBakeryLauncher.exe
XCOPY /S /E /C /I %BaseDir%\PEBakery\bin\Release %DestDir%\Binary
DEL %DestDir%\Binary\*.pdb
DEL %DestDir%\Binary\*.xml
DEL %DestDir%\Binary\*.config
DEL %DestDir%\Binary\*.db
RD /S /Q %DestDir%\Binary\Database
RD /S /Q %DestDir%\Database
COPY %BaseDir%\LICENSE %DestDir%\Binary
COPY %BaseDir%\LICENSE.GPLv3 %DestDir%\Binary
ENDLOCAL