/*
	Copyright (C) 2016-2021 Hajin Jang
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
#include "targetver.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

// C++ Runtime Headers
#include <string>
#include <sstream>
#include <vector>
#include <map>
#include <regex>

// C Runtime Headers
#include <cstdint>

// Local Headers
#include "NetLaunch.h"
#include "NetVersion.h"
#include "NetDetector.h"
#include "SysArch.h"
#include "Helper.h"

NetFxDetector::NetFxDetector(NetVersion& targetVer) :
	NetDetector(targetVer)
{
	// NetFxDetector supports .NET Framework 4.5+
	if (_targetVer < NetVersion(4, 5))
		NetLaunch::printError(L"The launcher is able to detect .NET Framework Runtime 4.5 or later.", true);
}

NetFxDetector::~NetFxDetector()
{

}

bool NetFxDetector::isInstalled()
{ // https://docs.microsoft.com/en-US/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed
	HKEY hKey = static_cast<HKEY>(INVALID_HANDLE_VALUE);
	const wchar_t* ndpPath = L"SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full";
	const wchar_t* ndpValue = L"Release";
	DWORD revision = 0;
	DWORD dwordSize = sizeof(DWORD);
	bool ret = false;

	if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, ndpPath, 0, KEY_READ | KEY_WOW64_64KEY, &hKey) != ERROR_SUCCESS)
		return false;

	if (RegQueryValueExW(hKey, ndpValue, NULL, NULL, (LPBYTE)&revision, &dwordSize) != ERROR_SUCCESS)
		goto out;

	if (getReleaseMinValue() <= revision)
		ret = true;

out:
	RegCloseKey(hKey);
	return ret;
}

void NetFxDetector::downloadRuntime(bool exitAfter)
{
	std::wstring url = getInstallerUrl();

	std::wostringstream woss;
	woss << L"PEBakery requires .NET Framework ";
	woss << _targetVer.toStr(false);
	woss << L" or later.";
	std::wstring errMsg = woss.str();

	woss.clear();
	woss.str(L"");
	woss << L"Install .NET Framework ";
	woss << _targetVer.toStr(false);
	std::wstring errCap = woss.str();

	NetLaunch::printErrorAndOpenUrl(errMsg, errCap, url, exitAfter);
}

DWORD NetFxDetector::getReleaseMinValue()
{
	DWORD minValue = UINT16_MAX;

	// For 4.5+
	if (_targetVer < NetVersion(4, 5))
		return minValue;

	uint16_t minor = _targetVer.getMinor();
	uint16_t patch = _targetVer.getPatch();
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

const std::wstring NetFxDetector::getInstallerUrl()
{
	// Strange enough, Microsoft does not provide offline installer for .NET Framework 4.5.
	if (_targetVer == NetVersion(4, 5))
		return L"https://dotnet.microsoft.com/download/dotnet-framework/thank-you/net45-web-installer";

	// Ex) https://dotnet.microsoft.com/download/dotnet-framework/thank-you/net48-offline-installer
	std::wostringstream woss;
	woss << L"https://dotnet.microsoft.com/download/dotnet-framework/thank-you/net";
	woss << _targetVer.getMajor();
	woss << _targetVer.getMinor();
	if (0 < _targetVer.getPatch())
		woss << _targetVer.getPatch();
	woss << L"-offline-installer";
	return woss.str();
}

NetCoreDetector::NetCoreDetector(NetVersion& targetVer, bool checkDesktopRuntime) :
	NetDetector(targetVer), _checkDesktopRuntime(checkDesktopRuntime)
{
	// NetCoreDetector supports .NET Core 2.1+
	// Desktop runtime have been provided since .NET Core 3.0.
	if (checkDesktopRuntime)
	{
		if (_targetVer < NetVersion(3, 0))
			NetLaunch::printError(L"The launcher is able to detect .NET Core Desktop Runtime 3.0 or later.", true);
	}
	else
	{
		if (_targetVer < NetVersion(2, 1))
			NetLaunch::printError(L"The launcher is able to detect .NET Core Runtime 2.1 or later.", true);
	}
}

NetCoreDetector::~NetCoreDetector()
{

}

bool NetCoreDetector::isInstalled()
{
	// Stage 1) Check registry to make sure a runtime of proper architecture is installed.
	// Check if the subkey HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\{Arch} exists.
	// Note) .NET Core SDK creates HKLM\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\{Arch}:InstallLocation on registry, but runtime does not.
	// Value example) 5.0.5 / 6.0.0-preview.3.21201.4

	const wchar_t* arch = SysArch::toStr(SysArch::getCpuArch());
	std::wstring subKeyPath = std::wstring(L"SOFTWARE\\dotnet\\Setup\\InstalledVersions\\") + arch;

	// Check if HKLM\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\{Arch} exists by opening it.
	{
		HKEY hKey = static_cast<HKEY>(INVALID_HANDLE_VALUE);
		if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, subKeyPath.c_str(), 0, KEY_READ | KEY_WOW64_64KEY, &hKey) != ERROR_SUCCESS)
			return false;
		RegCloseKey(hKey);
	}
	
	// Stage 2) Inspect stdout of `dotnet list-runtimes` command.

	// Read list of runtimes from `dotnet` command.
	std::map<std::string, std::vector<NetVersion>> rtMap = listRuntimes();
	
	// Check `Microsoft.NETCore.App` and `Microsoft.WindowsDesktop.App`.
	auto checkVer = [rtMap](const std::string& key, NetVersion& targetVer) -> bool
	{
		auto nit = rtMap.find(key);
		if (nit == rtMap.cend())
			return false;
		
		bool success = false;
		std::vector<NetVersion> versions = nit->second;
		for (NetVersion& v : versions)
		{
			if (targetVer.isCompatible(v))
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

void NetCoreDetector::downloadRuntime(bool exitAfter)
{
	std::wstring url = getInstallerUrl();
	std::wstring netCoreStr = getNetCoreString();

	std::wostringstream woss;
	woss << L"PEBakery requires ";
	woss << netCoreStr << L" ";
	if (_checkDesktopRuntime)
		woss << L"Desktop ";
	woss << L"Runtime ";
	woss << _targetVer.toStr(false);
	woss << L".";
	std::wstring errMsg = woss.str();

	woss.clear();
	woss.str(L"");
	woss << L"Install ";
	woss << netCoreStr << L" ";
	woss << _targetVer.toStr(false);
	if (_checkDesktopRuntime)
		woss << L" Desktop";
	woss << L" Runtime";
	std::wstring errCap = woss.str();

	NetLaunch::printErrorAndOpenUrl(errMsg, errCap, url, exitAfter);
}

std::map<std::string, std::vector<NetVersion>> NetCoreDetector::listRuntimes()
{
	/*
	> dotnet list-runtimes
	Microsoft.AspNetCore.App 5.0.5 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
	Microsoft.AspNetCore.App 6.0.0-preview.3.21201.13 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
	Microsoft.NETCore.App 5.0.5 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
	Microsoft.NETCore.App 6.0.0-preview.3.21201.4 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
	Microsoft.WindowsDesktop.App 6.0.0-preview.3.21201.3 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
	*/
	// https://docs.microsoft.com/ko-kr/windows/win32/procthread/creating-a-child-process-with-redirected-input-and-output?redirectedfrom=MSDN
	std::map<std::string, std::vector<NetVersion>> rtMap;

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

	BOOL ret = CreateProcessW(NULL, dotnet, NULL, NULL, TRUE, CREATE_NO_WINDOW, NULL, NULL, &si, &pi);

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
	std::string rtiStr;
	{
		std::ostringstream oss;
		readFromPipe(oss, hChildStdOutRd);
		CloseHandle(hChildStdOutRd);
		rtiStr = oss.str();
	}

	// Build rtMap
	const char* rtiPtr = rtiStr.c_str();
	while (1)
	{
		std::string line;
		rtiPtr = Helper::tokenize(rtiPtr, "\r\n", line);
		if (rtiPtr == nullptr)
			break;

		std::string key;
		NetVersion ver;
		if (parseRuntimeInfoLine(line, key, ver) == false)
			continue;

		auto it = rtMap.find(key);
		if (it == rtMap.end())
		{ // key is new 
			std::vector<NetVersion> versions;
			versions.push_back(ver);
			rtMap[key] = versions;
		}
		else
		{
			std::vector<NetVersion>& versions = it->second;
			versions.push_back(ver);
		}
	}
	
	return rtMap;
}

void NetCoreDetector::readFromPipe(std::ostringstream& destStream, HANDLE hSrcPipe)
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
bool NetCoreDetector::parseRuntimeInfoLine(const std::string& line, std::string& key, NetVersion& ver)
{
	// Ex)
	/*
	> dotnet list-runtimes
	Microsoft.AspNetCore.App 5.0.5 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
	Microsoft.AspNetCore.App 6.0.0-preview.3.21201.13 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
	Microsoft.NETCore.App 5.0.5 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
	Microsoft.NETCore.App 6.0.0-preview.3.21201.4 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
	Microsoft.WindowsDesktop.App 6.0.0-preview.3.21201.3 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
	*/

	const char* linePtr = line.c_str();
	char* nextPtr = NULL;

	// Read key
	linePtr = Helper::tokenize(linePtr, " ", key);
	if (linePtr == nullptr)
		return false;

	// Read version
	std::string verStr;
	linePtr = Helper::tokenize(linePtr, ' ', verStr);
	if (linePtr == nullptr)
		return false;

	// Parse version
	if (NetVersion::parse(verStr, ver) == false)
		return false;

	return true;
}

std::wstring NetCoreDetector::getNetCoreString()
{
	const NetVersion nameChangeVer(5, 0);

	std::wostringstream woss;
	woss << L".NET";
	if (_targetVer < nameChangeVer)
		woss << L" Core";
	return woss.str();
}

// Return installer url of .NET Core Windows Desktop Runtime.
const std::wstring NetCoreDetector::getInstallerUrl()
{
	ArchVal arch = SysArch::getCpuArch();
	const wchar_t* archStr = SysArch::toStr(arch);
	if (archStr == nullptr)
		NetLaunch::printError(L"Unsupported processor architecure!");

	// Patch number is only used on generating download urls.
	// [Pre .NET 5]
	// Ex) .NET Core Runtime: https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-3.1.5-windows-x64-installer
	// Ex) Desktop Runtime:   https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-desktop-3.1.5-windows-x64-installer
	// [.NET 5+]
	// Ex) .NET Runtime:      https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-5.0.5-windows-arm64-installer
	// Ex) Desktop Runtime:   https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-5.0.5-windows-x64-installer
	// [Preview]
	// Ex) .NET Runtime:      https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-6.0.0-preview.3-windows-x64-installer
	// Ex) Desktop Runtime:   https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-6.0.0-preview.3-windows-arm64-installer
	std::wstring verStr = _targetVer.toStr(false);
	std::wostringstream woss;
	woss << L"https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-";
	if (_checkDesktopRuntime)
		woss << L"desktop-";
	woss << verStr;
	woss << L"-windows-";
	woss << archStr;
	woss << L"-installer";
	return woss.str();
}

