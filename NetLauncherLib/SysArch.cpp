

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

typedef BOOL(WINAPI LPFN_ISWOW64PROCESS2) (HANDLE hProcess, USHORT* pProcessMachine, USHORT* pNativeMachine);

ArchVal SysArch::getCpuArch()
{
	HMODULE hModule = NULL;
	if (GetModuleHandleExW(0, L"kernel32", &hModule) == 0)
		return getCpuArchGetNativeSystemInfo();

	ArchVal ret = ArchVal::UNKNOWN;
	if (GetProcAddress(hModule, "IsWow64Process2") != nullptr) // Host supports IsWow64Process2 (Win 10 v1511+)
		ret = getCpuArchIsWow64Process2();
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

ArchVal SysArch::getCpuArchIsWow64Process2()
{
	USHORT wProcessMachine = 0;
	USHORT wNativeMachine = 0;

	if (IsWow64Process2(GetCurrentProcess(), &wProcessMachine, &wNativeMachine) == 0)
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
