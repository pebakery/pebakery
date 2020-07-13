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

// C++ Runtime Headers
#include <string>

// Local Headers
#include "PEParser.h"

class Helper
{
private:
public:
	static wchar_t* GetParameters(wchar_t* cmdLine);
	static void PrintError(const std::wstring& errMsg, bool exitAfter = true);
	static void PrintError(const std::wstring& errMsg, const std::wstring& errCaption, bool exitAfter = true);
	static void PrintErrorAndOpenUrl(const std::wstring& errMsg, const std::wstring& errCaption, const std::wstring& url, bool exitAfter = true);
	static void OpenUrl(const std::wstring& url);
	static PROC_ARCH GetProcArch();
	static const wchar_t* GetProcArchStr();
	static const wchar_t* GetProcArchStr(PROC_ARCH procArch);
	static const char* Tokenize(const char* str, const char token, std::string& out);
	static const wchar_t* Tokenize(const wchar_t* wstr, const wchar_t token, std::wstring& out);
	static const char* Tokenize(const char* str, const std::string& token, std::string& out);
	static const wchar_t* Tokenize(const wchar_t* wstr, const std::wstring& token, std::wstring& out);
};
