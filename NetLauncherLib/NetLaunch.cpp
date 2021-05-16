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

#include "targetver.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlwapi.h>
#include <shellapi.h>

// C++ Runtime Headers
#include <iostream>

// Local Headers
#include "NetLaunch.h"

// Get start point of argv[1] from command line
wchar_t* NetLaunch::getCmdParams(wchar_t* cmdLine)
{
	wchar_t* cmdParam = nullptr;

	// Case 1 : Simplest form of 'single param', no space
	// Ex) calc.exe
	if (cmdLine == nullptr || StrChrW(cmdLine, L' ') == nullptr)
	{
		cmdParam = nullptr;
	}
	else // It is 'multiple params' OR 'single param with quotes'
	{
		if (StrChrW(cmdLine, L'\"') == nullptr)
		{
			// Case 2 : 'multiple params' without quotes
			// Ex) notepad.exe Notepad-UTF8.txt
			cmdParam = StrChrW(cmdLine, L' ');
		}
		else
		{
			// Detect if first parameter has quotes
			if (StrChrW(cmdLine, L'\"') == cmdLine)
			{
				wchar_t* cmdLeftQuote = nullptr; // Start of first parameter
				wchar_t* cmdRightQuote = nullptr; // End of first parameter
				cmdLeftQuote = StrChrW(cmdLine, L'\"');
				cmdRightQuote = StrChrW(cmdLeftQuote + 1, L'\"');

				if (StrChrW(cmdRightQuote + 1, L' ') == nullptr)
				{
					// Case 3 : Single param with quotes on first param
					// Ex) "Simple Browser.exe"
					cmdParam = nullptr;
				}
				else
				{
					// Case 4 : Multiple param with quotes on first param
					// Ex) "Simple Browser.exe" joveler.kr
					cmdParam = StrChrW(cmdRightQuote + 1, L' '); // Spaces between cmdLeftQuote and cmdRightQuote must be ignored
				}
			}
			else
			{
				// Case 5 : Multiple param, but no quotes on first param
				// Ex) notepad.exe "Notepad UTF8.txt"
				cmdParam = StrChrW(cmdLine, L' ');
			}
		}
	}

	return cmdParam;
}

void NetLaunch::printError(const std::wstring& errMsg, bool exitAfter)
{
	std::wcerr << errMsg << std::endl;
	MessageBoxW(nullptr, errMsg.c_str(), L"Error", MB_OK | MB_ICONERROR);
	if (exitAfter)
		exit(1);
}

void NetLaunch::printError(const std::wstring& errMsg, const std::wstring& errCaption, bool exitAfter)
{
	std::wcerr << errMsg << std::endl;
	MessageBoxW(nullptr, errMsg.c_str(), errCaption.c_str(), MB_OK | MB_ICONERROR);
	if (exitAfter)
		exit(1);
}

void NetLaunch::printErrorAndOpenUrl(const std::wstring& errMsg, const std::wstring& errCaption, const std::wstring& url, bool exitAfter)
{
	std::wcerr << errMsg << std::endl;
	MessageBoxW(nullptr, errMsg.c_str(), errCaption.c_str(), MB_OK | MB_ICONERROR);
	openUrl(url);
	if (exitAfter)
		exit(1);
}

void NetLaunch::openUrl(const std::wstring& url)
{
	if (0 < url.size())
		ShellExecuteW(nullptr, nullptr, url.c_str(), nullptr, nullptr, SW_SHOWNORMAL);
}


bool NetLaunch::launchExe(const std::wstring& exePath, const std::wstring& baseDir, const wchar_t* cmdParams, const std::wstring& errMsg)
{
	// According to MSDN, ShellExecute's return value can be casted only to int.
	// In mingw, size_t casting should be used to evade [-Wpointer-to-int-cast] warning.
	int hRes = (int)(size_t)ShellExecuteW(NULL, NULL, exePath.c_str(), cmdParams, baseDir.c_str(), SW_SHOWNORMAL);
	if (hRes <= 32)
	{
		printError(errMsg, true);
		return false;
	}
	else
	{
		return true;
	}
}

bool NetLaunch::launchDll(const std::wstring& dllPath, const std::wstring& baseDir, const wchar_t* cmdParams, const std::wstring& errMsg)
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
		printError(errMsg, true);
		return false;
	}
	else
	{
		return true;
	}
}
