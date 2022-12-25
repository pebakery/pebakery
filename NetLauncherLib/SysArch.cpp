/*
	Copyright (C) 2016-2022 Hajin Jang
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

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlwapi.h>
#include <shellapi.h>
#include <wow64apiset.h>

// C++ Runtime Headers
#include <iostream>

// Local Headers
#include "Helper.h"
#include "PEParser.h"
#include "SysArch.h"

ArchVal SysArch::getCpuArch()
{
	HMODULE hModule = NULL;
	if (GetModuleHandleExW(0, L"kernel32", &hModule) == 0)
		return getCpuArchGetNativeSystemInfo();

	ArchVal ret = ArchVal::UNKNOWN;
	LPFN_ISWOW64PROCESS2 funcPtr = reinterpret_cast<LPFN_ISWOW64PROCESS2>(GetProcAddress(hModule, "IsWow64Process2"));
	if (funcPtr != nullptr) // Host supports IsWow64Process2 (Win 10 v1511+)
		ret = getCpuArchIsWow64Process2(funcPtr);
	else // Host is Win 7, 8, 8.1 or 10 v1507
		ret = getCpuArchGetNativeSystemInfo();

	FreeLibrary(hModule);
	return ret;
}

ArchVal SysArch::getCpuArchGetNativeSystemInfo()
{
	SYSTEM_INFO si;
	GetNativeSystemInfo(&si);

	ArchVal ret = ArchVal::UNKNOWN;
	switch (si.wProcessorArchitecture)
	{
	case PROCESSOR_ARCHITECTURE_INTEL:
		ret = ArchVal::X86;
		break;
	case PROCESSOR_ARCHITECTURE_AMD64:
		ret = ArchVal::X64;
		break;
	case PROCESSOR_ARCHITECTURE_ARM:
		ret = ArchVal::ARM;
		break;
	case PROCESSOR_ARCHITECTURE_ARM64:
		ret = ArchVal::ARM64;
		break;
	}

	return ret;
}

ArchVal SysArch::getCpuArchIsWow64Process2(LPFN_ISWOW64PROCESS2 funcPtr)
{
	USHORT wProcessMachine = 0;
	USHORT wNativeMachine = 0;

	if (funcPtr == nullptr)
		return ArchVal::UNKNOWN;
	if (funcPtr(GetCurrentProcess(), &wProcessMachine, &wNativeMachine) == 0)
		return ArchVal::UNKNOWN;

	return toArchVal(wNativeMachine);
}

ArchVal SysArch::getProcArch()
{
	SYSTEM_INFO si;
	GetSystemInfo(&si);

	ArchVal ret = ArchVal::UNKNOWN;
	switch (si.wProcessorArchitecture)
	{
	case PROCESSOR_ARCHITECTURE_INTEL:
		ret = ArchVal::X86;
		break;
	case PROCESSOR_ARCHITECTURE_AMD64:
		ret = ArchVal::X64;
		break;
	case PROCESSOR_ARCHITECTURE_ARM:
		ret = ArchVal::ARM;
		break;
	case PROCESSOR_ARCHITECTURE_ARM64:
		ret = ArchVal::ARM64;
		break;
	}
	return ret;
}

ArchVal SysArch::toArchVal(WORD wIamgeFileMachine)
{
	ArchVal ret = ArchVal::UNKNOWN;
	switch (wIamgeFileMachine)
	{
	case IMAGE_FILE_MACHINE_I386:
		ret = ArchVal::X86;
		break;
	case IMAGE_FILE_MACHINE_AMD64:
		ret = ArchVal::X64;
		break;
	case IMAGE_FILE_MACHINE_ARMNT:
		ret = ArchVal::ARM;
		break;
	case IMAGE_FILE_MACHINE_ARM64:
		ret = ArchVal::ARM64;
		break;
	}
	return ret;
}

const wchar_t* SysArch::toStr(ArchVal arch)
{
	const wchar_t* str = nullptr;

	switch (arch)
	{
	case ArchVal::X86:
		str = L"x86";
		break;
	case ArchVal::X64:
		str = L"x64";
		break;
	case ArchVal::ARM:
		str = L"arm";
		break;
	case ArchVal::ARM64:
		str = L"arm64";
		break;
	}

	return str;
}
