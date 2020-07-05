#pragma once

#include "Var.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
// #include <WinDef.h>
#include <Windows.h>

// C++ Runtime Headers
#include <string>
#include <vector>
#include <map>

// Local Headers
#include "Version.h"

#ifdef CHECK_NETFX
class NetFxDetector
{
private:
	const DWORD MIN_NETFX_RELEASE_VALUE = 461808;
	const wchar_t* NETFX_INSTALLER_URL = L"https://go.microsoft.com/fwlink/?LinkId=863265";
	const wchar_t* ERR_MSG_INSTALL_NETFX = L"PEBakery requires .NET Framework 4.7.2 or later.";
	const wchar_t* ERR_CAP_INSTALL_NETFX = L"Install .NET Framework 4.7.2";
public:
	NetFxDetector();
	bool IsInstalled();
	void ExitAndDownload();
};

#endif

#ifdef CHECK_NETCORE
constexpr WORD NETCORE_VER_MAJOR = 3;
constexpr WORD NETCORE_VER_MINOR = 1;
// Patch number is only used on generating download urls
constexpr WORD NETCORE_VER_PATCH = 5;

class NetCoreDetector
{
private:
	// Functions
	static std::map<std::string, std::vector<Version>> ListRuntimes();
	static void ReadFromPipe(std::ostringstream& destStream, HANDLE hSrcPipe);
	static bool ParseRuntimeInfoLine(std::string& line, std::string& key, Version& ver);
	std::wstring GetInstallerUrl();

	// Variables
	Version _targetVer;

	// Const
	const wchar_t* ERR_MSG_INSTALL_NETCORE = L"PEBakery requires .NET Core Desktop Runtime 3.1.";
	const wchar_t* ERR_CAP_INSTALL_NETCORE = L"Install .NET Core 3.1 Desktop Runtime";

public:	
	NetCoreDetector(Version& targetVer);
	bool IsInstalled();
	void ExitAndDownload();
};
#endif