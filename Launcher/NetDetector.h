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

#pragma once

#include "Var.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

// C++ Runtime Headers
#include <string>
#include <vector>
#include <map>

// Local Headers
#include "Version.h"

class NetDetector
{
protected:
	Version _targetVer;
	virtual const std::wstring GetInstallerUrl() = 0;
public:
	NetDetector(Version& targetVer) : 
		_targetVer(targetVer) { }
	virtual ~NetDetector() { }
	virtual bool IsInstalled() = 0;
	virtual void DownloadRuntime(bool exitAfter = true) = 0;
};

#ifdef CHECK_NETFX
/**
 * @breif Detect if .NET Framework 4.5 or later is installed.
 */
class NetFxDetector : public NetDetector
{
private:
	DWORD GetReleaseMinValue();
protected:
	virtual const std::wstring GetInstallerUrl();
public:
	NetFxDetector(Version& targetVer);
	virtual ~NetFxDetector();
	virtual bool IsInstalled();
	virtual void DownloadRuntime(bool exitAfter = true);
};

#endif

#ifdef CHECK_NETCORE
/**
 * @breif Detect if .NET Core 2.1 or later is installed.
 */
class NetCoreDetector : public NetDetector
{
private:
	bool _checkDesktopRuntime;

	static std::map<std::string, std::vector<Version>> ListRuntimes();
	static void ReadFromPipe(std::ostringstream& destStream, HANDLE hSrcPipe);
	static bool ParseRuntimeInfoLine(std::string& line, std::string& key, Version& ver);
protected:
	virtual const std::wstring GetInstallerUrl();
public:	
	NetCoreDetector(Version& targetVer, bool checkDesktopRuntime);
	virtual ~NetCoreDetector();
	virtual bool IsInstalled();
	virtual void DownloadRuntime(bool exitAfter = true);
};
#endif
