#pragma once

#include "Var.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
// #include <WinDef.h>
#include <Windows.h>

// C++ Runtime Headers
#include <string>

class Version
{
private:
	WORD _major;
	WORD _minor;
	WORD _patch;

public:
	// Constructors
	Version();
	Version(WORD major, WORD minor);
	Version(WORD major, WORD minor, WORD patch);

	// Member Functions
	WORD GetMajor();
	WORD GetMinor();
	WORD GetPatch();
	std::wstring ToString(bool excludePatch = false);
	bool IsEqual(Version& rhs, bool excludePatch = false);

	// Static Functions
	static bool Parse(const std::string& str, Version& ver);
	static bool Parse(const std::wstring& str, Version& ver);

	// Operator Overloading
	bool operator==(Version& rhs);
	bool operator!=(Version& rhs);
};
