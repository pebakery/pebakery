

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
#include "SysArch.h"

ArchVal SysArch::getCpuArch()
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
