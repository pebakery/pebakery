/*
	Copyright (C) 2020 Hajin Jang
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

// Custom Constants
#include "Var.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

// C++ Runtime Headers
#include <string>

// C Runtime Headers
#include <cstdint>

// Local Headers
#include "Helper.h"
#include "Version.h"
#include "PEParser.h"

enum class PROC_ARCH
{
	UNKNOWN = 0,
	X86,
	X64,
	ARM,
	ARM64,
};

class PEParser
{
private:
	std::wstring _filePath;

	PROC_ARCH _arch;
	int _bitness; // PE32 or PE32+?
	uint16_t _subsys; // Windows Subsystem
	uint16_t _chars; // Characteristics
public:
	// Constructor and Destructor
	PEParser(const std::wstring& filePath);
	~PEParser();

	// Parse and Utilities
	bool ParseFile();
	static int ArchToBitness(PROC_ARCH arch);

	// Getters
	PROC_ARCH GetArch() { return _arch; }
	int GetBitness() { return _bitness; }
	uint16_t GetSubSys() { return _subsys; }
	uint16_t GetCharacteristics() { return _chars; }
	bool IsDll() { return _chars & IMAGE_FILE_DLL; }
};

