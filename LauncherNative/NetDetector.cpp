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

// Local Headers
#include "Helper.h"
#include "Version.h"
#include "NetDetector.h"

using namespace std;

#ifdef CHECK_NETFX
NetFxDetector::NetFxDetector()
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

	if (MIN_NETFX_RELEASE_VALUE <= revision)
		ret = true;

out:
	RegCloseKey(hKey);
	return ret;
}

void NetFxDetector::ExitAndDownload()
{
	Helper::PrintErrorAndOpenUrl(ERR_MSG_INSTALL_NETFX, ERR_CAP_INSTALL_NETFX, NETFX_INSTALLER_URL);
}
#endif

#ifdef CHECK_NETCORE
NetCoreDetector::NetCoreDetector(Version& targetVer) :
	_targetVer(targetVer)
{ }

bool NetCoreDetector::IsInstalled()
{
	// Used method: Invoking `dotnet list-runtimes` command.
	// - .NET Core SDK creates HKLM\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\{Arch}:InstallLocation on registry.
	//   But .NET Core runtime does not create such registry value.

	// Read list of runtimes from `dotnet` command
	map<string, vector<Version>> rtMap = ListRuntimes();
	
	// Check `Microsoft.NETCore.App` and `Microsoft.WindowsDesktop.App`
	auto checkVer = [rtMap](const string& key, Version& targetVer) -> bool
	{
		map<string, vector<Version>>::const_iterator nit = rtMap.find(key);
		if (nit == rtMap.cend())
			return false;
		
		bool success = false;
		vector<Version> versions = nit->second;
		for (Version& v : versions)
		{
			// Do not compare patch version.
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
	installed &= checkVer("Microsoft.WindowsDesktop.App", _targetVer);
	return installed;
}

void NetCoreDetector::ExitAndDownload()
{
	wstring url = GetInstallerUrl();
	Helper::PrintErrorAndOpenUrl(ERR_MSG_INSTALL_NETCORE, ERR_CAP_INSTALL_NETCORE, url);
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
	ostringstream oss;
	ReadFromPipe(oss, hChildStdOutRd);

	// Build rtMap
	string rtiStr = oss.str();

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

	const DWORD ansiBufSize = 512;
	char ansiBuf[ansiBufSize];
	memset(ansiBuf, 0, sizeof(ansiBuf));
	while (1)
	{
		ret = ReadFile(hSrcPipe, ansiBuf, ansiBufSize - 1, &readBytes, NULL);
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
	

	/*
	regex r = regex("([a-zA-Z\\.]+) ([0-9]+)\\.([0-9]+)\\.([0-9]+) (\\[.+\\])");
	smatch m;

	if (!regex_match(line, m, r))
		return false;

	for (auto& sm : m)
	{
		sm.matched;
	}
	*/
}

// Return installer url of .NET Core Windows Desktop Runtime.
wstring NetCoreDetector::GetInstallerUrl()
{
	WORD procArch = Helper::GetProcArch();
	const wchar_t* procArchStr = Helper::GetProcArchStr(procArch);
	if (procArchStr == nullptr)
		Helper::PrintError(L"Unsupported processor architecure!");

	// Ex) L"https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-desktop-3.1.5-windows-x64-installer"
	wstring verStr = _targetVer.ToString(false);
	wostringstream woss;
	woss << L"https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-desktop-";
	woss << verStr;
	woss << L"-windows-";
	woss << procArchStr;
	woss << L"-installer";
	return woss.str();
}
#endif
