#pragma once

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
// #include <WinDef.h>
#include <Windows.h>

// C++ Runtime Headers
#include <string>

class Helper
{
private:
public:
	static wchar_t* GetParameters(wchar_t* cmdLine);
	static void PrintError(const std::wstring& errMsg);
	static void PrintErrorAndOpenUrl(const std::wstring& errMsg, const std::wstring& errCaption, const std::wstring& url);
	static WORD GetProcArch();
	static const wchar_t* GetProcArchStr(WORD procArch);
	static const char* Tokenize(const char* str, const char token, std::string& out);
	static const wchar_t* Tokenize(const wchar_t* wstr, const wchar_t token, std::wstring& out);
	static const char* Tokenize(const char* str, const std::string& token, std::string& out);
	static const wchar_t* Tokenize(const wchar_t* wstr, const std::wstring& token, std::wstring& out);
};

