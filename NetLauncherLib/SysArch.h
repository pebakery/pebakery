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

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

// C++ Runtime Headers
#include <vector>

enum class ArchVal
{
	UNKNOWN = 0,
	X86,
	X64,
	ARM,
	ARM64,
};

typedef BOOL(WINAPI* LPFN_ISWOW64PROCESS2) (HANDLE hProcess, USHORT* pProcessMachine, USHORT* pNativeMachine);

class SysArch
{
private:
	static ArchVal getCpuArchGetNativeSystemInfo();
	static ArchVal getCpuArchIsWow64Process2(LPFN_ISWOW64PROCESS2 funcPtr);
public:
	static ArchVal getCpuArch();
	static ArchVal getProcArch();
	static ArchVal toArchVal(WORD wIamgeFileMachine);
	static const wchar_t* toStr(ArchVal arch);
	static std::vector<ArchVal> getWowCompatibleArch(ArchVal hostArch);
	static bool isWoWCompatibleArch(ArchVal hostArch, ArchVal procArch);
};
