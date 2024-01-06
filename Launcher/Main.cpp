/*
	Copyright (C) 2016-2023 Hajin Jang
	Licensed under MIT License.

	MIT License

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
	SOFTWARE.
*/

// Constants
#include "targetver.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlwapi.h>
#include <shellapi.h>

// C++ Runtime Headers
#include <string>
#include <sstream>

// Resource Headers
#include "resource.h"

// Local Headers
#include "NetLaunch.h"
#include "NetVersion.h"
#include "NetDetector.h"
#include "PEParser.h"
#include "BuildVars.h"

// Prototypes
bool GetPEBakeryPath(std::wstring& baseDir, std::wstring& exePath, std::wstring& dllPath);
bool LaunchPEBakeryExe(const std::wstring& baseDir, const std::wstring& exePath, const wchar_t* cmdParams);
bool LaunchPEBakeryDll(const std::wstring& baseDir, const std::wstring& dllPath, const wchar_t* cmdParams);

int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
	_In_opt_ HINSTANCE hPrevInstance,
	_In_ LPWSTR    lpCmdLine,
	_In_ int       nCmdShow)
{
	UNREFERENCED_PARAMETER(hPrevInstance);
	UNREFERENCED_PARAMETER(lpCmdLine);

	// [Stage 01] Check .NET installation.
#if BUILD_MODE == BUILD_NETFX
	// Check if required version of .NET Framework is installed.
	NetVersion fxVer = NetVersion(NETFX_TARGET_VER_MAJOR, NETFX_TARGET_VER_MINOR, NETFX_TARGET_VER_PATCH);
	NetFxDetector fxDetector = NetFxDetector(fxVer);
	if (!fxDetector.isInstalled())
		fxDetector.downloadRuntime(true);
#elif BUILD_MODE == BUILD_NETCORE_RT_DEPENDENT
	// Check if required version of .NET Core is installed.
	// It is reported that .NET runtime somtetimes break minor-level forward compatibility.
	NetVersion coreVer = NetVersion(NETCORE_TARGET_VER_MAJOR, NETCORE_TARGET_VER_MINOR, NETCORE_TARGET_VER_PATCH);
	NetCoreDetector coreDetector = NetCoreDetector(coreVer, true);
	if (!coreDetector.isInstalled())
		coreDetector.downloadRuntime(true);
#endif

	// [Stage 02] Check and run PEBakery.
	// Get absolute path of PEBakery.exe.
	std::wstring baseDir;
	std::wstring pebExePath;
	std::wstring pebDllPath;
	if (GetPEBakeryPath(baseDir, pebExePath, pebDllPath) == false)
		NetLaunch::printError(L"Failed to retrieve PEBakery path.", true);

	// Parse Argument linces
	bool launched = false;
	bool archMatch = true;
	wchar_t* cmdParams = NetLaunch::getCmdParams(GetCommandLineW());

	// [Stage 03] Check and run PEBakery binary.
#if BUILD_MODE == BUILD_NETFX || BUILD_MODE == BUILD_NETCORE_SELF_CONTAINED
	// Run if PEBakery.exe exists.
	if (PathFileExistsW(pebExePath.c_str()))
	{
		// Do not check if PEBakery.exe matches the current processor architecture.
		// Ex) x86 build is compatible with x64 and arm64 machine.
		// Also, linking PEParser increases file size of the PEBakeryLauncher.exe.
		launched = LaunchPEBakeryExe(baseDir, pebExePath, cmdParams);
	}

#elif BUILD_MODE == BUILD_NETCORE_RT_DEPENDENT
	// Run if PEBakery.exe exists -> then try running PEBakery.dll
	if (PathFileExistsW(pebExePath.c_str()))
	{
		// Parse a header of the PEBakery.exe.
		PEParser parser;
		if (parser.parseFile(pebExePath.c_str()) == false)
			NetLaunch::printError(L"PEBakery.exe is corrupted.", true);

		// Check and run native PE exe file.
		if (parser.isNet())
			NetLaunch::printError(L"PEBakery.exe is not a .NET PE Executable.", true);

		// Check if PEBakery.exe matches the current processor architecture.
		// If not, launch PEBakery.dll.
		if (SysArch::getCpuArch() == parser.getArch())
			launched = LaunchPEBakeryExe(baseDir, pebExePath, cmdParams);
		else
			archMatch = false;
	}

	// Run if PEBakery.dll exists.
	if (!launched && PathFileExistsW(pebDllPath.c_str()))
		launched = LaunchPEBakeryDll(baseDir, pebDllPath, cmdParams);
#endif
	
	if (!launched)
	{
		if (!archMatch)
			NetLaunch::printError(L"PEBakery.exe is corrupted.", true);
		else
			NetLaunch::printError(L"Unable to find PEBakery.", true);
	}

	return 0;
}

// Constants
constexpr size_t MAX_PATH_LONG = 32768;

bool GetPEBakeryPath(std::wstring& baseDir, std::wstring& exePath, std::wstring& dllPath)
{
	auto wstrDeleter = [](wchar_t* ptr) { delete[] ptr; };
	std::unique_ptr<wchar_t[], decltype(wstrDeleter)> absPathPtr(new wchar_t[MAX_PATH_LONG], wstrDeleter);
	wchar_t* buffer = absPathPtr.get();

	// Get absolute path of PEBakeryLauncher.exe
	DWORD absPathLen = GetModuleFileNameW(NULL, buffer, MAX_PATH_LONG);
	if (absPathLen == 0)
	{
		NetLaunch::printError(L"Unable to query absolute path of PEBakeryLauncher.exe", true);
		return false;
	}
	buffer[MAX_PATH_LONG - 1] = '\0'; // NULL guard for Windows XP

	// Build baseDir
	wchar_t* lastDirSepPos = StrRChrW(buffer, NULL, L'\\');
	if (lastDirSepPos == NULL)
	{
		NetLaunch::printError(L"Unable to find base directory.", true);
		return false;
	}
	lastDirSepPos[0] = '\0';
	baseDir = std::wstring(buffer);

	// Build pebakeryPath
	exePath = baseDir + L"\\Binary\\PEBakery.exe";
	dllPath = baseDir + L"\\Binary\\PEBakery.dll";

	return true;
}

bool LaunchPEBakeryExe(const std::wstring& baseDir, const std::wstring& exePath, const wchar_t* cmdParams)
{
	// According to MSDN, ShellExecute's return value can be casted only to int.
	// In mingw, size_t casting should be used to evade [-Wpointer-to-int-cast] warning.
	int hRes = (int)(size_t)ShellExecuteW(NULL, NULL, exePath.c_str(), cmdParams, baseDir.c_str(), SW_SHOWNORMAL);
	if (hRes <= 32)
	{
		NetLaunch::printError(L"Unable to launch PEBakery.", true);
		return false;
	}
	else
	{
		return true;
	}
}

bool LaunchPEBakeryDll(const std::wstring& baseDir, const std::wstring& dllPath, const wchar_t* cmdParams)
{
	std::wstring paramStr = dllPath;
	if (cmdParams != nullptr)
	{
		paramStr.append(L" ");
		paramStr.append(cmdParams);
	}
	// Run `dotnet <PEBakery.dll> <params>` as Administrator
	int hRes = (int)(size_t)ShellExecuteW(NULL, L"runas", L"dotnet", paramStr.c_str(), baseDir.c_str(), SW_HIDE);
	if (hRes <= 32)
	{
		NetLaunch::printError(L"Unable to launch PEBakery.", true);
		return false;
	}
	else
	{
		return true;
	}
}
