// Constants
#include "Var.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <strsafe.h>
#include <shlwapi.h>
#include <shellapi.h>

// C++ Runtime Headers
#include <iostream>

// Local Headers
#include "Helper.h"

using namespace std;

// Get start point of argv[1] from command line
wchar_t* Helper::GetParameters(wchar_t* cmdLine)
{
	WCHAR* cmdRawLine = cmdLine;
	WCHAR* cmdParam = NULL;

	// Case 1 : Simplest form of 'single param', no space
	// Ex) calc.exe
	if (StrChrW(cmdRawLine, L' ') == NULL)
	{
		cmdParam = NULL;
	}
	else // It is 'multiple params' OR 'single param with quotes'
	{
		if (StrChrW(cmdRawLine, L'\"') == NULL)
		{
			// Case 2 : 'multiple params' without quotes
			// Ex) notepad.exe Notepad-UTF8.txt
			cmdParam = StrChrW(cmdRawLine, L' ');
		}
		else
		{
			// Detect if first parameter has quotes
			if (StrChrW(cmdRawLine, L'\"') == cmdRawLine)
			{
				wchar_t* cmdLeftQuote = NULL; // Start of first parameter
				wchar_t* cmdRightQuote = NULL; // End of first parameter
				cmdLeftQuote = StrChrW(cmdRawLine, L'\"');
				cmdRightQuote = StrChrW(cmdLeftQuote + 1, L'\"');

				if (StrChrW(cmdRightQuote + 1, L' ') == NULL)
				{
					// Case 3 : Single param with quotes on first param
					// Ex) "Simple Browser.exe"
					cmdParam = NULL;
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
				cmdParam = StrChrW(cmdRawLine, L' ');
			}
		}
	}

	return cmdParam;
}

void Helper::PrintError(const std::wstring& errMsg)
{
	wcerr << errMsg << endl;
	MessageBoxW(NULL, errMsg.c_str(), L"Error", MB_OK | MB_ICONERROR);
	exit(1);
}

void Helper::PrintErrorAndOpenUrl(const std::wstring& errMsg, const std::wstring& errCaption, const std::wstring& url)
{
	wcerr << errMsg << endl;
	MessageBoxW(NULL, errMsg.c_str(), errCaption.c_str(), MB_OK | MB_ICONERROR);
	if (0 < url.size())
		ShellExecuteW(NULL, NULL, url.c_str(), NULL, NULL, SW_SHOWNORMAL);
	exit(1);
}

WORD Helper::GetProcArch()
{
	SYSTEM_INFO si;
	GetNativeSystemInfo(&si);
	return si.wProcessorArchitecture;
}

const wchar_t* Helper::GetProcArchStr(WORD procArch)
{
	const wchar_t* str = nullptr;

	switch (procArch)
	{
	case PROCESSOR_ARCHITECTURE_INTEL:
		str = L"x86";
		break;
	case PROCESSOR_ARCHITECTURE_AMD64:
		str = L"x64";
		break;
	case PROCESSOR_ARCHITECTURE_ARM:
		str = L"arm";
		break;
	case PROCESSOR_ARCHITECTURE_ARM64:
		str = L"arm64";
		break;
	}

	return str;
}

const char* Helper::Tokenize(const char* str, const char token, std::string& out)
{
	const char* before = str;
	const char* after = before;

	after = StrChrA(before, token);
	if (after == nullptr)
		return nullptr;
	out = string(before, after - before);
	return after + 1;
}

const wchar_t* Helper::Tokenize(const wchar_t* wstr, const wchar_t token, std::wstring& out)
{
	const wchar_t* before = wstr;
	const wchar_t* after = before;

	after = StrChrW(before, token);
	if (after == nullptr)
		return nullptr;
	out = wstring(before, after - before);
	return after + 1;
}

const char* Helper::Tokenize(const char* str, const string& token, std::string& out)
{
	if (str == nullptr)
		return nullptr;

	const char* before = str;
	const char* after = before;

	after = StrStrA(before, token.c_str());
	if (after == nullptr)
		return nullptr;
	out = string(before, after - before);
	return after + token.size();
}

const wchar_t* Helper::Tokenize(const wchar_t* wstr, const wstring& token, std::wstring& out)
{
	const wchar_t* before = wstr;
	const wchar_t* after = before;

	after = StrStrW(before, token.c_str());
	if (after == nullptr)
		return nullptr;
	out = wstring(before, after - before);
	return after + token.size();
}