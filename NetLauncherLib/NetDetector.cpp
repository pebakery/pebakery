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

// Custom Constants
#include "targetver.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlwapi.h>

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
#include <iostream>

constexpr auto MAX_REG_KEY_LENGTH = 255;
constexpr auto REG_VALUENAME_BUF_LENGTH = 2048;
constexpr size_t MAX_PATH_LONG = 32768;

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
		case 1:
			minValue = 533320;
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
	// NetCoreDetector supports .NET 5+
	// Desktop runtime have been provided since .NET Core 3.0.
	if (checkDesktopRuntime)
	{
		if (_targetVer < NetVersion(5, 0))
			NetLaunch::printError(L"The launcher is able to detect .NET Desktop Runtime 5.0 or later.", true);
	}
	else
	{
		if (_targetVer < NetVersion(5, 0))
			NetLaunch::printError(L"The launcher is able to detect .NET Runtime 5.0 or later.", true);
	}
}

NetCoreDetector::~NetCoreDetector()
{

}

bool NetCoreDetector::isInstalled()
{
	// Check `Microsoft.NETCore.App` and `Microsoft.WindowsDesktop.App`.
	auto checkVer = [](const std::map<std::wstring, std::vector<NetVersion>>& rtMap, const std::wstring& key, NetVersion& targetVer) -> bool
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

	// Check registry to make sure a runtime of the proper architecture is installed. 
	// Value example) 5.0.5 / 6.0.0-preview.3.21201.4
	std::wstring installLoc;
	std::map<std::wstring, std::vector<NetVersion>> regRtMap;
	if (regListRuntimes(installLoc, regRtMap) == false || installLoc.size() == 0)
		return false;

	bool installed = true;
	installed &= checkVer(regRtMap, NET_CORE_ID, _targetVer);
	if (_checkDesktopRuntime)
		installed &= checkVer(regRtMap, WINDOWS_DESKTOP_RUNTIME_ID, _targetVer);
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
	woss << L" (";
	woss << SysArch::toStr(SysArch::getCpuArch());
	woss << L").";
	std::wstring errMsg = woss.str();

	woss.clear();
	woss.str(L"");
	woss << L"Install ";
	woss << netCoreStr << L" ";
	woss << _targetVer.toStr(false);
	if (_checkDesktopRuntime)
		woss << L" Desktop";
	woss << L" Runtime (";
	woss << SysArch::toStr(SysArch::getCpuArch());
	woss << L")";
	std::wstring errCap = woss.str();

	NetLaunch::printErrorAndOpenUrl(errMsg, errCap, url, exitAfter);
}

bool NetCoreDetector::regListRuntimes(std::wstring& outInstallLoc, std::map<std::wstring, std::vector<NetVersion>>& outRtMap)
{
	// Stage 1) Check registry to make sure a runtime of proper architecture is installed.
	// Check if the subkey HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\{Arch} exists.
	// 
	// Note) In .NET Core 3.1, the SDK KLM\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\{Arch}:InstallLocation on registry, but runtime does not.
	//       In .NET 6, the SDK and runtime both creates registry entries.
	// Value example) 5.0.5 / 6.0.0-preview.3.21201.4

	/*
	[HKEY_LOCAL_MACHINE\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost]
	"Version"="7.0.3"
	"Path"="C:\\Program Files\\dotnet\\"

	[HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64]
	"InstallLocation"="C:\\Program Files\\dotnet\\"

	[HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\hostfxr]
	"Version"="7.0.3"

	[HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sdk]
	"7.0.103"=dword:00000001

	[HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx]

	[HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App]
	"3.1.32"=dword:00000001
	"6.0.14"=dword:00000001
	"7.0.3"=dword:00000001

	[HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App]
	"6.0.14"=dword:00000001
	"7.0.3"=dword:00000001
	*/

	const std::wstring nativeKeyRoot = L"SOFTWARE\\dotnet\\Setup\\InstalledVersions\\";
	const std::wstring wowKeyRoot = L"SOFTWARE\\WOW6432Node\\dotnet\\Setup\\InstalledVersions\\";
	const wchar_t* arch = SysArch::toStr(SysArch::getCpuArch());

	auto hKeyDeleter = [](HKEY hKey)
	{
		if (hKey != INVALID_HANDLE_VALUE && hKey != NULL)
			RegCloseKey(hKey);
	};

	bool success = false;

	// Try reading native reg subkey first, then WOW6432Node subkey.
	std::vector<std::wstring> subKeyRoots;
	subKeyRoots.push_back(std::wstring(nativeKeyRoot) + arch);
	subKeyRoots.push_back(std::wstring(wowKeyRoot) + arch);
	for (std::wstring& subKeyRoot : subKeyRoots)
	{
		// [Stage 1] Check SOFTWARE{\WOW6432Node\}dotnet\Setup\InstalledVersions\{Arch}:InstallLocation value
		{
			// Open and setup smart pointer for HKEY handle.
			HKEY hKey = static_cast<HKEY>(INVALID_HANDLE_VALUE);
			if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, subKeyRoot.c_str(), 0, KEY_READ | KEY_WOW64_64KEY, &hKey) != ERROR_SUCCESS)
				continue;
			std::unique_ptr<std::remove_pointer<HKEY>::type, decltype(hKeyDeleter)> hKeyPtr(hKey, hKeyDeleter);

			// Read `InstallLocation` value
			auto wstrDeleter = [](wchar_t* ptr) { delete[] ptr; };
			std::unique_ptr<wchar_t[], decltype(wstrDeleter)> installLocPtr(new wchar_t[MAX_PATH_LONG], wstrDeleter);
			wchar_t* installLocBuf = installLocPtr.get();
			if (installLocBuf == nullptr)
				continue;
			installLocBuf[0] = '\0';

			DWORD valueSize = MAX_PATH_LONG;
			if (RegQueryValueExW(hKeyPtr.get(), L"InstallLocation", NULL, NULL, reinterpret_cast<LPBYTE>(installLocBuf), &valueSize) != ERROR_SUCCESS)
				continue;

			outInstallLoc = installLocBuf;
		}

		// [Stage 2] Search for subkeys of SOFTWARE\{WOW6432Node\}dotnet\Setup\InstalledVersions\{Arch}\sharedfx
		// e.g. Microsoft.NETCore.App, Microsoft.WindowsDesktop.App
		std::vector<std::wstring> fxIds;
		{
			std::wstring subKeyPath = subKeyRoot + L"\\sharedfx";

			// Open and setup smart pointer for HKEY handle
			HKEY hKey = static_cast<HKEY>(INVALID_HANDLE_VALUE);
			if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, subKeyPath.c_str(), 0, KEY_READ | KEY_WOW64_64KEY, &hKey) != ERROR_SUCCESS)
				continue;
			std::unique_ptr<std::remove_pointer<HKEY>::type, decltype(hKeyDeleter)> hKeyPtr(hKey, hKeyDeleter);

			// Enumerate subkey - search for Microsoft.NETCore.App, Microsoft.WindowsDesktop.App
			LSTATUS retCode = ERROR_SUCCESS;
			for (DWORD rIdx = 0; retCode == ERROR_SUCCESS; rIdx++)
			{
				wchar_t keyNameBuf[MAX_REG_KEY_LENGTH] = { 0, };
				DWORD keyNameLen = MAX_REG_KEY_LENGTH;

				retCode = RegEnumKeyExW(hKeyPtr.get(), rIdx, keyNameBuf, &keyNameLen, NULL, NULL, NULL, NULL);
				if (retCode != ERROR_SUCCESS)
					break;

				fxIds.push_back(keyNameBuf);
			}
		}

		// [Stage 3] Retrieve installed versions from SOFTWARE\{WOW6432Node\}dotnet\Setup\InstalledVersions\{Arch}\sharedfx\{fxId}
		// Ex: 3.1.13, 6.0.14
		for (std::wstring fxId : fxIds)
		{
			std::wstring subKeyPath = subKeyRoot + L"\\sharedfx\\" + fxId;

			// Open and setup smart pointer for HKEY handle
			HKEY hKey = static_cast<HKEY>(INVALID_HANDLE_VALUE);
			if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, subKeyPath.c_str(), 0, KEY_READ | KEY_WOW64_64KEY, &hKey) != ERROR_SUCCESS)
				continue;
			std::unique_ptr<std::remove_pointer<HKEY>::type, decltype(hKeyDeleter)> hKeyPtr(hKey, hKeyDeleter);

			// Enumerate valuenames in subkey
			LSTATUS retCode = ERROR_SUCCESS;
			for (DWORD rIdx = 0; retCode == ERROR_SUCCESS; rIdx++)
			{
				wchar_t valueNameBuf[REG_VALUENAME_BUF_LENGTH] = { 0, };
				DWORD valueNameLen = REG_VALUENAME_BUF_LENGTH;

				retCode = RegEnumValueW(hKeyPtr.get(), rIdx, valueNameBuf, &valueNameLen, NULL, NULL, NULL, NULL);
				if (retCode != ERROR_SUCCESS)
					break;

				// Parse version
				NetVersion ver;
				if (NetVersion::parse(valueNameBuf, ver) == false)
					continue;

				auto it = outRtMap.find(fxId);
				if (it == outRtMap.end())
				{ // key is new 
					std::vector<NetVersion> versions;
					versions.push_back(ver);
					outRtMap[fxId] = versions;
				}
				else
				{
					std::vector<NetVersion>& versions = it->second;
					versions.push_back(ver);
				}

				// If one or more values are found, then the function has succeeded.
				success = true;
			}
		}
	}

	return success;
}

bool NetCoreDetector::cliListRuntimes(std::wstring installLoc, std::map<std::wstring, std::vector<NetVersion>>& outRtMap)
{
	/*
	> dotnet list-runtimes
	Microsoft.AspNetCore.App 5.0.5 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
	Microsoft.AspNetCore.App 6.0.0-preview.3.21201.13 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
	Microsoft.NETCore.App 5.0.5 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
	Microsoft.NETCore.App 6.0.0-preview.3.21201.4 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
	Microsoft.WindowsDesktop.App 6.0.0-preview.3.21201.3 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
	*/
	// https://docs.microsoft.com/ko-kr/windows/win32/procthread/creating-a-child-process-with-redirected-input-and-output?redirectedfrom=MSD
	std::wstring appName = installLoc + L"dotnet.exe";
	std::wstring cmdLine = L"\"" + installLoc + L"dotnet.exe\" --list-runtimes";
	auto hDeleter = [](HANDLE handle)
	{
		if (handle != INVALID_HANDLE_VALUE && handle != NULL)
			CloseHandle(handle);
	};

	std::string rtiStr;
	{
		// Setup pipes
		SECURITY_ATTRIBUTES saAttr;
		memset(&saAttr, 0, sizeof(SECURITY_ATTRIBUTES));
		saAttr.nLength = sizeof(SECURITY_ATTRIBUTES);
		saAttr.bInheritHandle = TRUE;
		saAttr.lpSecurityDescriptor = NULL;

		HANDLE hChildStdInRd = NULL;
		HANDLE hChildStdInWr = NULL;
		HANDLE hChildStdOutRd = NULL;
		HANDLE hChildStdOutWr = NULL;
		
		if (!CreatePipe(&hChildStdOutRd, &hChildStdOutWr, &saAttr, 0))
			return false;
		if (!SetHandleInformation(hChildStdOutRd, HANDLE_FLAG_INHERIT, 0))
			return false;
		if (!CreatePipe(&hChildStdInRd, &hChildStdInWr, &saAttr, 0))
			return false;
		if (!SetHandleInformation(hChildStdInWr, HANDLE_FLAG_INHERIT, 0))
			return false;

		PROCESS_INFORMATION pi;
		STARTUPINFO si;
		memset(&pi, 0, sizeof(PROCESS_INFORMATION));
		memset(&si, 0, sizeof(STARTUPINFO));

		si.cb = sizeof(STARTUPINFO);
		si.hStdInput = hChildStdInRd;
		si.hStdOutput = hChildStdOutWr;
		si.hStdError = hChildStdOutWr;
		si.dwFlags |= STARTF_USESTDHANDLES;

		BOOL ret = CreateProcessW(appName.c_str(), const_cast<LPWSTR>(cmdLine.c_str()), NULL, NULL, TRUE, CREATE_NO_WINDOW, NULL, NULL, &si, &pi);

		// .NET Core Runtime or SDK is not installed, so dotnet.exe is not callable.
		if (ret == FALSE)
		{
			std::wcerr << L"CreateProcessW failed, appName(" << appName << L") cmdLine(" << cmdLine << L")" << std::endl;

			CloseHandle(hChildStdOutRd);
			CloseHandle(hChildStdOutWr);
			CloseHandle(hChildStdInRd);
			CloseHandle(hChildStdInWr);

			return false;
		}

		// Close unnecessary handles to the child process and its primary thread
		CloseHandle(pi.hProcess);
		CloseHandle(pi.hThread);

		// Close unnecessary handles to the stdin and stdout pipe.
		CloseHandle(hChildStdOutWr);
		CloseHandle(hChildStdInRd);
		CloseHandle(hChildStdInWr);

		// Read from child stdout read pipe
		std::ostringstream oss;
		readFromPipe(oss, hChildStdOutRd);
		rtiStr = oss.str();

		CloseHandle(hChildStdOutRd);
	}

	std::cerr << rtiStr << std::endl;

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
		std::wstring wkey = Helper::to_wstr(key);

		auto it = outRtMap.find(wkey);
		if (it == outRtMap.end())
		{ // key is a new one.
			std::vector<NetVersion> versions;
			versions.push_back(ver);
			outRtMap[wkey] = versions;
		}
		else
		{
			std::vector<NetVersion>& versions = it->second;
			versions.push_back(ver);
		}
	}
	
	return true;
}

bool NetCoreDetector::findDotnetLocationFromPath(std::wstring& outInstallLoc)
{
	constexpr size_t MAX_PATH_LONG = 32768;

	auto wstrDeleter = [](wchar_t* ptr) { delete[] ptr; };
	std::unique_ptr<wchar_t[], decltype(wstrDeleter)> pathPtr(new wchar_t[MAX_PATH_LONG], wstrDeleter);
	wchar_t* buffer = pathPtr.get();

	wchar_t* lpFilePart = nullptr;
	DWORD pathLen = SearchPathW(nullptr, L"dotnet", L".exe", MAX_PATH_LONG, buffer, &lpFilePart);
	if (pathLen == 0)
		return false; // dotnet.exe is not searchable from the PATH

	// trim "dotnet.exe" part and leave only directory (include trailing '\')
	if (lpFilePart == nullptr || lpFilePart < buffer)
		return false;
	size_t exeDirLen = lpFilePart - buffer;
	outInstallLoc.append(buffer, exeDirLen);
	return true;
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
	> dotnet --list-runtimes
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
	if (5 <= _targetVer.getMajor())
		woss << L"https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-";
	else
		woss << L"https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-";
	if (_checkDesktopRuntime)
		woss << L"desktop-";
	woss << verStr;
	woss << L"-windows-";
	woss << archStr;
	woss << L"-installer";
	return woss.str();
}

