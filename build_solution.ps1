$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuildPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | select-object -first 1
Write-Host "MSBuild Path: $msbuildPath"
if ($msbuildPath) {
    & $msbuildPath PEBakery.sln /restore /m /t:Build /p:Configuration=Debug /p:Platform=x64 /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary
} else {
    Write-Error "MSBuild.exe not found."
}
