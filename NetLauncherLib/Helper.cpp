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

// Constants
#include "targetver.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlwapi.h>
#include <shellapi.h>

// C++ Runtime Headers
#include <iostream>

// Local Headers
#include "Helper.h"
#include "PEParser.h"

const char* Helper::tokenize(const char* str, const char token, std::string& out)
{
	const char* before = str;
	const char* after = before;

	out = str;
	after = StrChrA(before, token);
	if (after == nullptr)
		return nullptr;
	out = std::string(before, after - before);
	return after + 1;
}

const wchar_t* Helper::tokenize(const wchar_t* wstr, const wchar_t token, std::wstring& out)
{
	const wchar_t* before = wstr;
	const wchar_t* after = before;

	out = wstr;
	after = StrChrW(before, token);
	if (after == nullptr)
		return nullptr;
	out = std::wstring(before, after - before);
	return after + 1;
}

const char* Helper::tokenize(const char* str, const std::string& token, std::string& out)
{
	if (str == nullptr)
		return nullptr;

	const char* before = str;
	const char* after = before;

	out = str;
	after = StrStrA(before, token.c_str());
	if (after == nullptr)
		return nullptr;
	out = std::string(before, after - before);
	return after + token.size();
}

const wchar_t* Helper::tokenize(const wchar_t* wstr, const std::wstring& token, std::wstring& out)
{
	const wchar_t* before = wstr;
	const wchar_t* after = before;

	out = wstr;
	after = StrStrW(before, token.c_str());
	if (after == nullptr)
		return nullptr;
	out = std::wstring(before, after - before);
	return after + token.size();
}

bool Helper::isWindows11orLater()
{
	// Check if the host system is Windows 11 or later (10.0.22000+)
	OSVERSIONINFOEXW osvi;
	memset(&osvi, 0, sizeof(OSVERSIONINFOEXW));
	osvi.dwOSVersionInfoSize = sizeof(OSVERSIONINFOEXW);
	osvi.dwMajorVersion = 10;
	osvi.dwMinorVersion = 0;
	osvi.dwBuildNumber = 22000;

	// Initialize the condition mask.
	DWORDLONG dwlConditionMask = 0;
	int op = VER_GREATER_EQUAL;
	VER_SET_CONDITION(dwlConditionMask, VER_MAJORVERSION, op);
	VER_SET_CONDITION(dwlConditionMask, VER_MINORVERSION, op);
	VER_SET_CONDITION(dwlConditionMask, VER_BUILDNUMBER, op);

	return VerifyVersionInfoW(&osvi, VER_MAJORVERSION | VER_MINORVERSION | VER_BUILDNUMBER, dwlConditionMask);
}

std::string Helper::to_str(const std::wstring& wstr)
{
	int cchRequired = WideCharToMultiByte(CP_ACP, 0, wstr.c_str(), -1, nullptr, 0, nullptr, nullptr);
	if (cchRequired == 0)
		return "";

	auto strDeleter = [](char* ptr) { delete[] ptr; };
	std::unique_ptr<char[], decltype(strDeleter)> strBufPtr(new char[cchRequired], strDeleter);
	char* strBuf = strBufPtr.get();
	strBuf[0] = '\0';

	int cchWritten = WideCharToMultiByte(CP_ACP, 0, wstr.c_str(), -1, strBuf, cchRequired, nullptr, nullptr);
	if (cchWritten == 0)
		return "";

	return strBuf;
}

std::wstring Helper::to_wstr(const std::string& str)
{
	int cchRequired = MultiByteToWideChar(CP_ACP, 0, str.c_str(), -1, nullptr, 0);
	if (cchRequired == 0)
		return L"";

	auto wstrDeleter = [](wchar_t* ptr) { delete[] ptr; };
	std::unique_ptr<wchar_t[], decltype(wstrDeleter)> wstrBufPtr(new wchar_t[cchRequired], wstrDeleter);
	wchar_t* wstrBuf = wstrBufPtr.get();
	wstrBuf[0] = L'\0';
	
	int cchWritten = MultiByteToWideChar(CP_ACP, 0, str.c_str(), -1, wstrBuf, cchRequired);
	if (cchWritten == 0)
		return L"";

	return wstrBuf;
}
