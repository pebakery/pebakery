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
