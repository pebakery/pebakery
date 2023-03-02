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

#include "argparse.hpp"

// Constants
#include "targetver.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlwapi.h>

// C++ Runtime Headers
#include <string>
#include <algorithm>
#include <set>
#include <vector>
#include <iostream>

// Resource Headers
#include "resource.h"

// Library Headers
#include "NetVersion.h"
#include "NetDetector.h"

enum class PrintMode
{
	HUMAN = 0,
	MINOR = 1,
	PATCH = 2,
};

void printHelp(const std::string& progName)
{
	std::cerr << ".NET Runtime Detector by Joveler v1.0.0" << std::endl << std::endl;
	std::cerr << "Usage: " << progName << " <args>" << std::endl;
	std::cerr << "  --req-major <MAJOR>" << std::endl;
	std::cerr << "  [--res-minor|--res-patch]" << std::endl;
	std::cerr << "  [--win-desktop]" << std::endl;
}

NetVersion findLatestVersion(const std::set<NetVersion>& versions)
{
	NetVersion latestMajorVer(0, 0, 0);
	for (const NetVersion& version : versions)
	{
		if (latestMajorVer < version)
			latestMajorVer = version;
	}
	return latestMajorVer;
}

bool filtertInstalledRuntime(const std::wstring& runtimeId, const std::map<std::wstring, std::vector<NetVersion>>& rtMap, int32_t majorVer, std::set<NetVersion>& outVers)
{
	NetVersion latestMajorVer(0, 0, 0);
	auto it = rtMap.find(runtimeId);
	if (it == rtMap.end())
	{
		std::wcerr << L"ERR: .NET [" << runtimeId << L"] runtime is not installed." << std::endl;
		return false;
	}

	outVers.clear();
	for (const NetVersion& version : it->second)
	{
		if (version.getMajor() != majorVer)
			continue;

		outVers.insert(version);
	}

	if (outVers.size() == 0)
	{
		std::wcerr << L"ERR: .NET [" << runtimeId << "] runtime v[" << majorVer << ".x] is not installed." << std::endl;
		return false;
	}

	return true;
}

int main(int argc, char* argv[])
{
	// [Stage 01] Check arguments
	std::string progName = argv[0];
	const std::string reqMajorParamKey = "--req-major";
	const std::string resMinorParamKey = "--res-minor";
	const std::string resPatchParamKey = "--res-patch";
	const std::string winDesktopParamKey = "--win-desktop";

	argparse::ArgumentParser argParser(progName, "1.0.0");
	argParser.add_argument(reqMajorParamKey)
		.required()
		.help("Major version of .NET runtime to check.");
	argParser.add_argument(resMinorParamKey)
		.help("Print only minor version.")
		.default_value(false)
		.implicit_value(true);
	argParser.add_argument(resPatchParamKey)
		.help("Print only patch version.")
		.default_value(false)
		.implicit_value(true);
	argParser.add_argument(winDesktopParamKey)
		.help("Also check Windows Desktop Runtime.")
		.default_value(false)
		.implicit_value(true);

	try
	{
		argParser.parse_args(argc, argv);
	}
	catch (const std::runtime_error& err)
	{
		std::cerr << err.what() << std::endl;
		std::cerr << argParser;
		exit(1);
	
	}

	// Request: --req-major (string)
	std::string reqMajorStr = argParser.get<std::string>(reqMajorParamKey);
	int32_t reqMajor = StrToIntA(reqMajorStr.c_str());
	if (reqMajor < 5)
	{
		std::cerr << reqMajorParamKey << " [" << reqMajor << "] is too low, use [5] or later." << std::endl << std::endl;
		exit(1);
	}

	// Response: <--res-minor|--res-patch|--res-human>
	PrintMode printMode = PrintMode::HUMAN;
	if (argParser.get<bool>(resMinorParamKey))
		printMode = PrintMode::MINOR;
	if (argParser.get<bool>(resPatchParamKey))
		printMode = PrintMode::PATCH;

	// Optional: --win-desktop (bool)
	bool checkWinDesktop = argParser.get<bool>(winDesktopParamKey);

	// [Stage 02] Check .NET runtimes
	std::wstring installLoc;
	std::map<std::wstring, std::vector<NetVersion>> rtMap;
	if (NetCoreDetector::regListRuntimes(installLoc, rtMap) == false)
	{
		std::wcerr << L"ERR: .NET Runtime is not installed." << std::endl;
		exit(1);
	}
	
	// Check installed .NET runtime versions	
	std::set<NetVersion> netCoreVerSet;
	std::set<NetVersion> netWinVerSet;
	bool foundVersion = filtertInstalledRuntime(NetCoreDetector::NET_CORE_ID, rtMap, reqMajor, netCoreVerSet);
	if (checkWinDesktop)
		foundVersion = filtertInstalledRuntime(NetCoreDetector::WINDOWS_DESKTOP_RUNTIME_ID, rtMap, reqMajor, netWinVerSet);

	if (foundVersion == false)
		exit(1);
	
	std::set<NetVersion> bothVerSet;
	if (checkWinDesktop)
		std::set_intersection(netCoreVerSet.begin(), netCoreVerSet.end(), netWinVerSet.begin(), netWinVerSet.end(), std::inserter(bothVerSet, bothVerSet.begin()));
	else
		bothVerSet = netCoreVerSet;
	NetVersion latestNetVer = findLatestVersion(bothVerSet);
	if (latestNetVer.getMajor() == 0)
	{
		std::wcerr << L"ERR: .NET Runtime v[" << reqMajor << L".x] is not installed." << std::endl;
		exit(1);
	}

	// [Stage 03] Print detected .NET versions
	switch (printMode)
	{
	case PrintMode::HUMAN:
		std::wcout << latestNetVer.toStr();
		break;
	case PrintMode::MINOR:
		std::wcout << latestNetVer.getMinor();
		break;
	case PrintMode::PATCH:
		std::wcout << latestNetVer.getPatch();
		break;
	}
	std::wcout << std::endl;
	return 0;

}
