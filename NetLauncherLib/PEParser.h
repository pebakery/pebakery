/*
	Copyright (C) 2020-2023 Hajin Jang
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
#include "targetver.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

// C++ Runtime Headers
#include <string>

// C Runtime Headers
#include <cstdint>

// Local Headers
#include "SysArch.h"

enum class PEFormat
{
	UNKNOWN = 0,
	PE32 = 32,
	PE32_PLUS = 64,
};

class PEParser
{
private:
	PEFormat _format; // PE32 or PE32+?
	ArchVal _arch; // Processor Architecutre
	uint16_t _subsys; // Windows Subsystem
	uint16_t _characteristics; // Characteristics
	bool _isNet; // Is .NET assembly?

	// Parse
	bool ParseDosHeader(const HANDLE hFile, size_t& outPeHeaderPos);
	bool ParsePECoffHeader(const HANDLE hFile, const size_t peHeaderPos, size_t& outOptHeaderPos);
	bool ParsePEOptionalHeader(const HANDLE hFile, const size_t optHeaderPos);
	static bool IsImageDataDirectoryValid(const IMAGE_DATA_DIRECTORY& dir);
public:
	// Constructor and Destructor
	PEParser();
	~PEParser();

	// Parse
	bool parseFile(const std::wstring& filePath);

	// Getters
	PEFormat getFormat() { return _format; }
	ArchVal getArch() { return _arch; }
	uint16_t getSubSys() { return _subsys; }
	uint16_t getCharacteristics() { return _characteristics; }

	// Utilities
	static int archToBitness(ArchVal arch);
	bool isDll() { return _characteristics & IMAGE_FILE_DLL; }
	bool isNet() { return _isNet; }
};
