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

// C++ Runtime Headers
#include <string>

class NetLaunch
{
public:
	static wchar_t* getCmdParams(wchar_t* cmdLine);
	static void printError(const std::wstring& errMsg, bool exitAfter = true);
	static void printError(const std::wstring& errMsg, const std::wstring& errCaption, bool exitAfter = true);
	static void printErrorAndOpenUrl(const std::wstring& errMsg, const std::wstring& errCaption, const std::wstring& url, bool exitAfter = true);
	static void openUrl(const std::wstring& url);
	static bool launchExe(const std::wstring& exePath, const std::wstring& baseDir, const wchar_t* cmdParams, const std::wstring& errMsg);
	static bool launchDll(const std::wstring& dllPath, const std::wstring& baseDir, const wchar_t* cmdParams, const std::wstring& errMsg);
};

