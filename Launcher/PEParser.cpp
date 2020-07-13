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

// Custom Constants
#include "Var.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <strsafe.h>
#include <shlwapi.h>
#include <shellapi.h>

// C++ Runtime Headers
#include <string>
#include <sstream>
#include <vector>
#include <map>
#include <regex>

// C Runtime Headers
#include <cstdint>

// Local Headers
#include "Helper.h"
#include "Version.h"
#include "NetDetector.h"
#include "PEParser.h"

using namespace std;

PEParser::PEParser(const wstring& filePath) :
	_filePath(filePath), 
	_arch(PROC_ARCH::UNKNOWN), _bitness(0), _subsys(0), _chars(0)
{
}

PEParser::~PEParser()
{
}

bool PEParser::ParseFile()
{
	// Open file handle.
	HANDLE hFile = CreateFileW(_filePath.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
	if (hFile == INVALID_HANDLE_VALUE)
		return false;

	// Set smart pointer for RAII
	auto handleDeleter = [](void* ptr) { CloseHandle(static_cast<HANDLE>(ptr)); };
	unique_ptr<void, decltype(handleDeleter)> hFilePtr(hFile, handleDeleter);

	// Get file size
	int64_t fileSize = 0;
	{
		LARGE_INTEGER llFileSize;
		if (GetFileSizeEx(hFile, &llFileSize) == FALSE)
			return false;
		fileSize = llFileSize.QuadPart;
	}

	uint8_t buffer[4096];

	// Read and parse MZ header
	uint32_t peHeaderPos = 0;
	uint32_t peFileHeaderPos = 0;
	{
		memset(buffer, 0, sizeof(buffer));

		DWORD readBytes = 0;
		const size_t mzHeaderSize = sizeof(IMAGE_DOS_HEADER);
		if (ReadFile(hFile, buffer, mzHeaderSize, &readBytes, nullptr) == FALSE)
			return false;
		if (readBytes != mzHeaderSize)
			return false;

		IMAGE_DOS_HEADER* dosHeader = reinterpret_cast<IMAGE_DOS_HEADER*>(buffer);

		// Check MZ magic;
		if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE)
			return false;

		peHeaderPos = dosHeader->e_lfanew; // PE\0\0
	}
	 
	// Read and parse NT COFF Header (PE signature and IMAGE_FILE_HEADER)
	// IMAGE_NT_HEADER struct is different in Win32 / Win64 build, so let's directly access it.
	uint32_t peOptHeaderPos = 0;
	size_t peOptHeaderSize = 0;
	{
		memset(buffer, 0, sizeof(buffer));

		// Seek NT header
		LARGE_INTEGER distance;
		distance.QuadPart = peHeaderPos;
		if (SetFilePointerEx(hFile, distance, nullptr, FILE_BEGIN) == FALSE)
			return false;

		// Read NT COFF header
		DWORD readBytes = 0;
		const size_t peHeaderSize = sizeof(DWORD) + sizeof(IMAGE_FILE_HEADER) + sizeof(WORD);
		if (ReadFile(hFile, buffer, peHeaderSize, &readBytes, nullptr) == FALSE)
			return false;
		if (readBytes != peHeaderSize)
			return false;

		// Check PE signature
		uint8_t* bufReadPtr = buffer;
		DWORD peSignature = *(reinterpret_cast<DWORD*>(bufReadPtr));
		if (peSignature != IMAGE_NT_SIGNATURE)
			return false;
		bufReadPtr += sizeof(DWORD);

		// Parse PE file header
		IMAGE_FILE_HEADER* peFileHeader = reinterpret_cast<IMAGE_FILE_HEADER*>(bufReadPtr);
		switch (peFileHeader->Machine)
		{
		case IMAGE_FILE_MACHINE_I386:
			_arch = PROC_ARCH::X86;
			break;
		case IMAGE_FILE_MACHINE_AMD64:
			_arch = PROC_ARCH::X64;
			break;
		case IMAGE_FILE_MACHINE_ARM:
		case IMAGE_FILE_MACHINE_ARMNT:
			_arch = PROC_ARCH::ARM;
			break;
		case IMAGE_FILE_MACHINE_ARM64:
			_arch = PROC_ARCH::ARM64;
			break;
		}
		_chars = peFileHeader->Characteristics;
		bufReadPtr += sizeof(IMAGE_FILE_HEADER);

		// Parse NT Optional Header Magic
		WORD ntOptMagic = *reinterpret_cast<WORD*>(bufReadPtr);
		switch (ntOptMagic)
		{
		case IMAGE_NT_OPTIONAL_HDR32_MAGIC: // PE32
			_bitness = 32;
			peOptHeaderSize = sizeof(IMAGE_OPTIONAL_HEADER32);
			break;
		case IMAGE_NT_OPTIONAL_HDR64_MAGIC: // PE32+
			_bitness = 64;
			peOptHeaderSize = sizeof(IMAGE_OPTIONAL_HEADER64);
			break;
		default:
			return false;
		}

		// Get position of NT optional header
		peOptHeaderPos = peHeaderPos + sizeof(DWORD) + sizeof(IMAGE_FILE_HEADER);
	}

	// Read and parse NT optional header 
	{
		memset(buffer, 0, sizeof(buffer));

		// Seek NT header
		LARGE_INTEGER distance;
		distance.QuadPart = peOptHeaderPos;
		if (SetFilePointerEx(hFile, distance, nullptr, FILE_BEGIN) == FALSE)
			return false;

		// Read NT optional header
		DWORD readBytes = 0;
		if (ReadFile(hFile, buffer, peOptHeaderSize, &readBytes, nullptr) == FALSE)
			return false;
		if (readBytes != peOptHeaderSize)
			return false;

		// Parse NT optional header
		switch (_bitness)
		{
		case 32:
			{
				IMAGE_OPTIONAL_HEADER32* optHeader32 = reinterpret_cast<IMAGE_OPTIONAL_HEADER32*>(buffer);
				_subsys = optHeader32->Subsystem;
			}
			break;
		case 64:
			{
				IMAGE_OPTIONAL_HEADER64* optHeader64 = reinterpret_cast<IMAGE_OPTIONAL_HEADER64*>(buffer);
				_subsys = optHeader64->Subsystem;
			}
			break;
		}
	}

	// Cleanup
	return true;
}

int PEParser::ArchToBitness(PROC_ARCH arch)
{
	int bitness = 0;
	switch (arch)
	{
	case PROC_ARCH::X86:
	case PROC_ARCH::ARM:
		bitness = 32;
		break;
	case PROC_ARCH::X64:
	case PROC_ARCH::ARM64:
		bitness = 64;
		break;
	}
	return bitness;
}
