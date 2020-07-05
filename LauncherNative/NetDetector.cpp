/*
	Copyright (C) 2016-2020 Hajin Jang
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

// Custom Constants
#include "Var.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <strsafe.h>
#include <shlwapi.h>
#include <shellapi.h>

// C++ Runtime Headers
#include <string>
#include <sstream>
#include <vector>
#include <map>
#include <regex>

// C Runtime Headers
#include <cstdint>

// Local Headers
#include "Helper.h"
#include "Version.h"
#include "NetDetector.h"

using namespace std;

#ifdef CHECK_NETFX
NetFxDetector::NetFxDetector(Version& targetVer) :
	NetDetector(targetVer)
{
	// NetFxDetector supports .NET Framework 4.5+
	if (_targetVer < Version(4, 5))
		Helper::PrintError(L"The launcher is able to detect .NET Framework Runtime 4.5 or later.", true);
}

NetFxDetector::~NetFxDetector()
{

}

bool NetFxDetector::IsInstalled()
{ // https://docs.microsoft.com/en-US/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed
	HKEY hKey = static_cast<HKEY>(INVALID_HANDLE_VALUE);
	const WCHAR* ndpPath = L"SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full";
	const WCHAR* ndpValue = L"Release";
	DWORD revision = 0;
	DWORD dwordSize = sizeof(DWORD);
	bool ret = false;

	if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, ndpPath, 0, KEY_READ | KEY_WOW64_64KEY, &hKey) != ERROR_SUCCESS)
		return false;

	if (RegQueryValueExW(hKey, ndpValue, NULL, NULL, (LPBYTE)&revision, &dwordSize) != ERROR_SUCCESS)
		goto out;

	if (GetReleaseMinValue() <= revision)
		ret = true;

out:
	RegCloseKey(hKey);
	return ret;
}

void NetFxDetector::DownloadRuntime(bool exitAfter)
{
	wstring url = GetInstallerUrl();

	wostringstream woss;
	woss << L"PEBakery requires .NET Framework ";
	woss << _targetVer.ToString(false);
	woss << L" or later.";
	wstring errMsg = woss.str();

	woss.clear();
	woss.str(L"");
	woss << L"Install .NET Framework ";
	woss << _targetVer.ToString(false);
	wstring errCap = woss.str();

	Helper::PrintErrorAndOpenUrl(errMsg, errCap, url, exitAfter);
}

DWORD NetFxDetector::GetReleaseMinValue()
{
	DWORD minValue = UINT16_MAX;

	// For 4.5+
	if (_targetVer < Version(4, 5))
		return minValue;

	uint16_t minor = _targetVer.GetMinor();
	uint16_t patch = _targetVer.GetPatch();
	switch (minor)
	{
	case 5:
		switch (patch)
		{
		case 0:
			minValue = 378389;
			break;
		case 1:
			minValue = 378675;
			break;
		case 2:
			minValue = 379893;
			break;
		}
		break;
	case 6:
		switch (patch)
		{
		case 0:
			minValue = 393295;
			break;
		case 1:
			minValue = 394254;
			break;
		case 2:
			minValue = 394802;
			break;
		}
		break;
	case 7:
		switch (patch)
		{
		case 0:
			minValue = 460798;
			break;
		case 1:
			minValue = 461308;
			break;
		case 2:
			minValue = 461808;
			break;
		}
		break;
	case 8:
		switch (patch)
		{
		case 0:
			minValue = 528040;
			break;
		}
		break;
	}

	return minValue;
}

const wstring NetFxDetector::GetInstallerUrl()
{
	// Strange enough, Microsoft does not provide offline installer for .NET Framework 4.5.
	if (_targetVer == Version(4, 5))
		return L"https://dotnet.microsoft.com/download/dotnet-framework/thank-you/net45-web-installer";

	// Ex) https://dotnet.microsoft.com/download/dotnet-framework/thank-you/net48-offline-installer
	wostringstream woss;
	woss << L"https://dotnet.microsoft.com/download/dotnet-framework/thank-you/net";
	woss << _targetVer.GetMajor();
	woss << _targetVer.GetMinor();
	if (0 < _targetVer.GetPatch())
		woss << _targetVer.GetPatch();
	woss << L"-offline-installer";
	return woss.str();
}
#endif

#ifdef CHECK_NETCORE
NetCoreDetector::NetCoreDetector(Version& targetVer, bool checkDesktopRuntime) :
	NetDetector(targetVer), _checkDesktopRuntime(checkDesktopRuntime)
{
	// NetCoreDetector supports .NET Core 2.1+
	// Desktop runtime have been provided since .NET Core 3.0.
	if (checkDesktopRuntime)
	{
		if (_targetVer < Version(3, 0))
			Helper::PrintError(L"The launcher is able to detect .NET Core Desktop Runtime 3.0 or later.", true);
	}
	else
	{
		if (_targetVer < Version(2, 1))
			Helper::PrintError(L"The launcher is able to detect .NET Core Runtime 2.1 or later.", true);
	}
}

NetCoreDetector::~NetCoreDetector()
{

}

bool NetCoreDetector::IsInstalled()
{
	// Used method: Invoking `dotnet list-runtimes` command.
	// - .NET Core SDK creates HKLM\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\{Arch}:InstallLocation on registry.
	//   But .NET Core runtime does not create such registry value.

	// Read list of runtimes from `dotnet` command.
	map<string, vector<Version>> rtMap = ListRuntimes();
	
	// Check `Microsoft.NETCore.App` and `Microsoft.WindowsDesktop.App`.
	auto checkVer = [rtMap](const string& key, Version& targetVer) -> bool
	{
		auto nit = rtMap.find(key);
		if (nit == rtMap.cend())
			return false;
		
		bool success = false;
		vector<Version> versions = nit->second;
		for (Version& v : versions)
		{
			// Do not compare patch version.
			// Patch number is only used on generating download urls.
			if (targetVer.IsEqual(v, true))
			{
				success = true;
				break;
			}
		}
		return success;
	};
	
	bool installed = true;
	installed &= checkVer("Microsoft.NETCore.App", _targetVer);
	if (_checkDesktopRuntime)
		installed &= checkVer("Microsoft.WindowsDesktop.App", _targetVer);
	return installed;
}

void NetCoreDetector::DownloadRuntime(bool exitAfter)
{
	wstring url = GetInstallerUrl();

	wostringstream woss;
	woss << L"PEBakery requires .NET Core ";
	if (_checkDesktopRuntime)
		woss << L"Desktop ";
	woss << L"Runtime ";
	woss << _targetVer.ToString(true);
	woss << L".";
	wstring errMsg = woss.str();

	woss.clear();
	woss.str(L"");
	woss << L"Install .NET Core ";
	woss << _targetVer.ToString(true);
	if (_checkDesktopRuntime)
		woss << L" Desktop";
	woss << L" Runtime";
	wstring errCap = woss.str();

	Helper::PrintErrorAndOpenUrl(errMsg, errCap, url, exitAfter);
}

map<string, vector<Version>> NetCoreDetector::ListRuntimes()
{
	// > dotnet list-runtimes
	// Microsoft.AspNetCore.App 3.1.5 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
	// Microsoft.NETCore.App 3.1.5 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
	// Microsoft.WindowsDesktop.App 3.1.5 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
	// https://docs.microsoft.com/ko-kr/windows/win32/procthread/creating-a-child-process-with-redirected-input-and-output?redirectedfrom=MSDN
	map<string, vector<Version>> rtMap;

	WCHAR dotnet[32] = L"dotnet --list-runtimes"; // lpCommandLine must not be a const wchar_t.

	// Setup pipes
	SECURITY_ATTRIBUTES saAttr;
	saAttr.nLength = sizeof(SECURITY_ATTRIBUTES);
	saAttr.bInheritHandle = TRUE;
	saAttr.lpSecurityDescriptor = NULL;
	HANDLE hChildStdInRd = NULL;
	HANDLE hChildStdInWr = NULL;
	HANDLE hChildStdOutRd = NULL;
	HANDLE hChildStdOutWr = NULL;
	if (!CreatePipe(&hChildStdOutRd, &hChildStdOutWr, &saAttr, 0))
		return rtMap;
	if (!SetHandleInformation(hChildStdOutRd, HANDLE_FLAG_INHERIT, 0))
		return rtMap;
	if (!CreatePipe(&hChildStdInRd, &hChildStdInWr, &saAttr, 0))
		return rtMap;
	if (!SetHandleInformation(hChildStdInWr, HANDLE_FLAG_INHERIT, 0))
		return rtMap;

	PROCESS_INFORMATION pi;
	STARTUPINFO si;
	memset(&pi, 0, sizeof(PROCESS_INFORMATION));
	memset(&si, 0, sizeof(STARTUPINFO));

	si.cb = sizeof(STARTUPINFO);
	si.hStdInput = hChildStdInRd;
	si.hStdOutput = hChildStdOutWr;
	si.hStdError = hChildStdOutWr;
	si.dwFlags |= STARTF_USESTDHANDLES;

	BOOL ret = CreateProcessW(NULL, dotnet, NULL, NULL, TRUE, 0, NULL, NULL, &si, &pi);

	// .NET Core Runtime or SDK is not installed, so dotnet.exe is not callable.
	if (ret == FALSE)
		return rtMap;

	// Close unnecessary handles to the child process and its primary thread
	CloseHandle(pi.hProcess);
	CloseHandle(pi.hThread);

	// Close unnecessary handles to the stdin and stdout pipe.
	CloseHandle(hChildStdOutWr);
	CloseHandle(hChildStdInRd);
	CloseHandle(hChildStdInWr);

	// Read from child stdout read pipe
	string rtiStr;
	{
		ostringstream oss;
		ReadFromPipe(oss, hChildStdOutRd);
		CloseHandle(hChildStdOutRd);
		rtiStr = oss.str();
	}

	// Build rtMap
	const char* rtiPtr = rtiStr.c_str();
	while (1)
	{
		string line;
		rtiPtr = Helper::Tokenize(rtiPtr, "\r\n", line);
		if (rtiPtr == nullptr)
			break;

		string key;
		Version ver;
		if (ParseRuntimeInfoLine(line, key, ver) == false)
			continue;

		auto it = rtMap.find(key);
		if (it == rtMap.end())
		{ // key is new 
			vector<Version> versions;
			versions.push_back(ver);
			rtMap[key] = versions;
		}
		else
		{
			vector<Version>& versions = it->second;
			versions.push_back(ver);
		}
	}
	
	return rtMap;
}

void NetCoreDetector::ReadFromPipe(ostringstream& destStream, HANDLE hSrcPipe)
{
	BOOL ret = TRUE;
	DWORD readBytes = 0;

	char ansiBuf[4096];
	memset(ansiBuf, 0, sizeof(ansiBuf));
	DWORD ansiBufSize = static_cast<DWORD>(sizeof(ansiBuf) - 1);
	while (1)
	{
		ret = ReadFile(hSrcPipe, ansiBuf, ansiBufSize, &readBytes, NULL);
		if (ret == FALSE || readBytes == 0)
			break;
		
		ansiBuf[readBytes] = '\0';
		destStream << ansiBuf;
	} 
}

// Return false on failure
bool NetCoreDetector::ParseRuntimeInfoLine(string& line, string& key, Version& ver)
{
	// Ex)
	// Microsoft.NETCore.App 3.1.5 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
	// Microsoft.WindowsDesktop.App 3.1.5 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]

	const char* linePtr = line.c_str();

	// Read key
	linePtr = Helper::Tokenize(linePtr, ' ', key);
	if (linePtr == nullptr)
		return false;

	// Read version
	string verStr;
	linePtr = Helper::Tokenize(linePtr, ' ', verStr);
	if (linePtr == nullptr)
		return false;

	// Parse version
	if (Version::Parse(verStr, ver) == false)
		return false;

	return true;
}

// Return installer url of .NET Core Windows Desktop Runtime.
const wstring NetCoreDetector::GetInstallerUrl()
{
	WORD procArch = Helper::GetProcArch();
	const wchar_t* procArchStr = Helper::GetProcArchStr(procArch);
	if (procArchStr == nullptr)
		Helper::PrintError(L"Unsupported processor architecure!");

	// Patch number is only used on generating download urls.
	// Ex) .NET Core Runtime: https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-3.1.5-windows-x64-installer
	// Ex) Desktop Runtime:   https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-desktop-3.1.5-windows-x64-installer
	wstring verStr = _targetVer.ToString(false);
	wostringstream woss;
	woss << L"https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-";
	if (_checkDesktopRuntime)
		woss << L"desktop-";
	woss << verStr;
	woss << L"-windows-";
	woss << procArchStr;
	woss << L"-installer";
	return woss.str();
}

#endif

