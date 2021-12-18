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

#pragma once

#include "targetver.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

// C++ Runtime Headers
#include <string>
#include <vector>
#include <map>

// Local Headers
#include "NetVersion.h"

class NetDetector
{
protected:
	NetVersion _targetVer;
	virtual const std::wstring getInstallerUrl() = 0;
public:
	NetDetector(NetVersion& targetVer) :
		_targetVer(targetVer) { }
	virtual ~NetDetector() { }
	virtual bool isInstalled() = 0;
	virtual void downloadRuntime(bool exitAfter = true) = 0;
};

/**
 * @breif Detect if .NET Framework 4.5 or later is installed.
 */
class NetFxDetector : public NetDetector
{
private:
	DWORD getReleaseMinValue();
protected:
	virtual const std::wstring getInstallerUrl();
public:
	NetFxDetector(NetVersion& targetVer);
	virtual ~NetFxDetector();
	virtual bool isInstalled();
	virtual void downloadRuntime(bool exitAfter = true);
};

/**
 * @breif Detect if .NET Core 2.1 or later is installed.
 */
class NetCoreDetector : public NetDetector
{
private:
	bool _checkDesktopRuntime;

	static std::map<std::string, std::vector<NetVersion>> listRuntimes();
	static void readFromPipe(std::ostringstream& destStream, HANDLE hSrcPipe);
protected:
	virtual const std::wstring getInstallerUrl();
public:	
	NetCoreDetector(NetVersion& targetVer, bool checkDesktopRuntime);
	virtual ~NetCoreDetector();
	virtual bool isInstalled();
	virtual void downloadRuntime(bool exitAfter = true);
	static bool parseRuntimeInfoLine(const std::string& line, std::string& key, NetVersion& ver);
};
