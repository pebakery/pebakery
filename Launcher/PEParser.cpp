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

static constexpr size_t BUF_SiZE = 4096;

#if BUILD_MODE == BUILD_NETCORE_RT_DEPENDENT || BUILD_MODE == BUILD_NETFX
PEParser::PEParser() :
	_format(PEFormat::UNKNOWN), _arch(ProcArch::UNKNOWN), 
	_subsys(0), _characteristics(0),
	_isNet(false)
{
}

PEParser::~PEParser()
{
}

bool PEParser::ParseFile(const wstring& filePath)
{
	// Open the file handle.
	HANDLE hFile = CreateFileW(filePath.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
	if (hFile == INVALID_HANDLE_VALUE)
		return false;

	// Set smart pointer for RAII
	auto handleDeleter = [](void* ptr) { CloseHandle(static_cast<HANDLE>(ptr)); };
	unique_ptr<void, decltype(handleDeleter)> hFilePtr(hFile, handleDeleter);

	// Read and parse MZ header
	size_t peHeaderPos = 0;
	if (!ParseDosHeader(hFile, peHeaderPos))
		return false;
	
	// Read and parse PE COFF Header (PE signature and IMAGE_FILE_HEADER)
	// IMAGE_NT_HEADER struct is different in Win32 / Win64 build, so let's directly access it.
	uint32_t optHeaderPos = 0;
	size_t peOptHeaderSize = 0;
	if (!ParsePECoffHeader(hFile, peHeaderPos, optHeaderPos))
		return false;

	// Read and parse NT optional header 
	if (!ParsePEOptionalHeader(hFile, optHeaderPos))
		return false;

	// Cleanup
	return true;
}

bool PEParser::ParseDosHeader(const HANDLE hFile, size_t& outCoffHeaderPos)
{
	// Allocate buffer
	uint8_t buffer[BUF_SiZE];
	memset(buffer, 0, sizeof(buffer));

	// Seek MZ header
	LARGE_INTEGER distance;
	distance.QuadPart = 0;
	if (SetFilePointerEx(hFile, distance, nullptr, FILE_BEGIN) == FALSE)
		return false;

	// Read and parse MZ header
	DWORD readBytes = 0;
	constexpr size_t mzHeaderSize = sizeof(IMAGE_DOS_HEADER);
	if (ReadFile(hFile, buffer, mzHeaderSize, &readBytes, nullptr) == FALSE)
		return false;
	if (readBytes != mzHeaderSize)
		return false;

	IMAGE_DOS_HEADER* dosHeader = reinterpret_cast<IMAGE_DOS_HEADER*>(buffer);

	// Check MZ magic;
	if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE)
		return false;

	// Get PE COFF header position
	outCoffHeaderPos = dosHeader->e_lfanew; // PE\0\0
	return true;
}

bool PEParser::ParsePECoffHeader(const HANDLE hFile, const size_t peHeaderPos, size_t& outOptHeaderPos)
{
	// Allocate buffer
	uint8_t buffer[BUF_SiZE];
	memset(buffer, 0, sizeof(buffer));

	// Seek NT header
	LARGE_INTEGER distance;
	distance.QuadPart = peHeaderPos;
	if (SetFilePointerEx(hFile, distance, nullptr, FILE_BEGIN) == FALSE)
		return false;

	// Read and parse PE COFF Header (PE signature and IMAGE_FILE_HEADER)
	// IMAGE_NT_HEADER struct is different in Win32 / Win64 build, so do not use it.
	// Instead, directly access it from buffer.

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

	// Parse NT COFF header
	IMAGE_FILE_HEADER* peFileHeader = reinterpret_cast<IMAGE_FILE_HEADER*>(bufReadPtr);
	switch (peFileHeader->Machine)
	{
	case IMAGE_FILE_MACHINE_I386:
		_arch = ProcArch::X86;
		break;
	case IMAGE_FILE_MACHINE_AMD64:
		_arch = ProcArch::X64;
		break;
	case IMAGE_FILE_MACHINE_ARMNT:
		_arch = ProcArch::ARM;
		break;
	case IMAGE_FILE_MACHINE_ARM64:
		_arch = ProcArch::ARM64;
		break;
	}
	_characteristics = peFileHeader->Characteristics;
	bufReadPtr += sizeof(IMAGE_FILE_HEADER);

	// Parse NT Optional Header Magic
	WORD ntOptMagic = *reinterpret_cast<WORD*>(bufReadPtr);
	switch (ntOptMagic)
	{
	case IMAGE_NT_OPTIONAL_HDR32_MAGIC: // PE32
		_format = PEFormat::PE32;
		// outOptHeaderSize = sizeof(IMAGE_OPTIONAL_HEADER32);
		break;
	case IMAGE_NT_OPTIONAL_HDR64_MAGIC: // PE32+
		_format = PEFormat::PE32_PLUS;
		// peOptHeaderSize = sizeof(IMAGE_OPTIONAL_HEADER64);
		break;
	default:
		return false;
	}

	// Get position of NT optional header
	outOptHeaderPos = peHeaderPos + sizeof(DWORD) + sizeof(IMAGE_FILE_HEADER);
	return true;
}

bool PEParser::ParsePEOptionalHeader(const HANDLE hFile, const size_t optHeaderPos)
{
	// Allocate buffer
	uint8_t buffer[BUF_SiZE];
	memset(buffer, 0, sizeof(buffer));

	// Seek PE optional header
	LARGE_INTEGER distance;
	distance.QuadPart = optHeaderPos;
	if (SetFilePointerEx(hFile, distance, nullptr, FILE_BEGIN) == FALSE)
		return false;

	// Get NT optional header size
	size_t optHeaderSize = 0;
	switch (_format)
	{
	case PEFormat::PE32:
		optHeaderSize = sizeof(IMAGE_OPTIONAL_HEADER32);
		break;
	case PEFormat::PE32_PLUS:
		optHeaderSize = sizeof(IMAGE_OPTIONAL_HEADER64);
		break;
	default:
		return false;
	}

	// Read PE optional header
	DWORD readBytes = 0;
	if (ReadFile(hFile, buffer, optHeaderSize, &readBytes, nullptr) == FALSE)
		return false;
	if (readBytes != optHeaderSize)
		return false;

	// Parse NT optional header
	if (_format == PEFormat::PE32)
	{
		IMAGE_OPTIONAL_HEADER32* optHeader = reinterpret_cast<IMAGE_OPTIONAL_HEADER32*>(buffer);
		_subsys = optHeader->Subsystem;

		// Check .NET descriptor directory
		IMAGE_DATA_DIRECTORY netDir = optHeader->DataDirectory[IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR];
		_isNet = IsImageDataDirectoryValid(netDir);
	}
	else if (_format == PEFormat::PE32)
	{
		IMAGE_OPTIONAL_HEADER64* optHeader = reinterpret_cast<IMAGE_OPTIONAL_HEADER64*>(buffer);
		_subsys = optHeader->Subsystem;

		// Check .NET descriptor directory
		IMAGE_DATA_DIRECTORY netDir = optHeader->DataDirectory[IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR];
		_isNet = IsImageDataDirectoryValid(netDir);
	}

	return true;
}

bool PEParser::IsImageDataDirectoryValid(const IMAGE_DATA_DIRECTORY& dir)
{
	return dir.VirtualAddress != 0 && dir.Size != 0;
}

int PEParser::ArchToBitness(ProcArch arch)
{
	int bitness = 0;
	switch (arch)
	{
	case ProcArch::X86:
	case ProcArch::ARM:
		bitness = 32;
		break;
	case ProcArch::X64:
	case ProcArch::ARM64:
		bitness = 64;
		break;
	}
	return bitness;
}
#endif
