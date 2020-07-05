#include "Version.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <strsafe.h>
#include <shlwapi.h>

// C++ Runtime Headers
#include <string>
#include <sstream>

// Local Headers
#include "Helper.h"

using namespace std;

Version::Version() :
	_major(0), _minor(0), _patch(0)
{ }

Version::Version(WORD major, WORD minor) :
	_major(major), _minor(minor), _patch(0)
{ }

Version::Version(WORD major, WORD minor, WORD patch) :
	_major(major), _minor(minor), _patch(patch)
{ }

WORD Version::GetMajor()
{
	return _major;
}

WORD Version::GetMinor()
{
	return _minor;
}

WORD Version::GetPatch()
{
	return _patch;
}

wstring Version::ToString(bool excludePatch)
{
	// Version String is used for displaying purpose only
	wostringstream woss;
	woss << _major;
	woss << L'.';
	woss << _minor;
	if (excludePatch == false)
	{
		woss << L'.';
		woss << _patch;
	}
	return woss.str();
}

bool Version::IsEqual(Version& rhs, bool excludePatch)
{
	bool isEqual = _major == rhs.GetMajor() && _minor == rhs.GetMinor();
	if (excludePatch == false)
		isEqual &= _patch == rhs.GetPatch();
	return isEqual;
}

bool Version::Parse(const std::string& str, Version& ver)
{
	string slice;

	// Read major
	const char* minorPtr = Helper::Tokenize(str.c_str(), '.', slice);
	if (minorPtr == nullptr)
		return false;
	WORD major = StrToIntA(slice.c_str());

	// Read minor and patch
	const char* patchPtr = Helper::Tokenize(minorPtr, '.', slice);
	if (patchPtr == nullptr)
	{ // No patch, Ex) 3.1
		WORD minor = StrToIntA(minorPtr);
		ver = Version(major, minor);
	}
	else
	{ // Found patch, Ex) 3.1.5
		WORD minor = StrToIntA(slice.c_str());
		WORD patch = StrToIntA(patchPtr);
		ver = Version(major, minor, patch);
	}

	return true;
}

bool Version::Parse(const std::wstring& wstr, Version& ver)
{
	const wchar_t* before = wstr.c_str();
	const wchar_t* after = before;
	wstring slice;

	// Read major
	after = Helper::Tokenize(before, '.', slice);
	if (after == nullptr)
		return false;
	WORD major = StrToIntW(slice.c_str());

	// Read minor
	after = Helper::Tokenize(before, '.', slice);
	if (after == nullptr)
		return false;
	WORD minor = StrToIntW(slice.c_str());

	// Read patch (if exists)
	after = Helper::Tokenize(before, '.', slice);
	if (after == nullptr)
	{ // Ex) 3.1
		ver = Version(major, minor);
	}
	else
	{ // Ex) 3.1.5
		WORD patch = StrToIntW(slice.c_str());
		ver = Version(major, minor, patch);
	}

	return true;
}

bool Version::operator==(Version& rhs)
{
	return IsEqual(rhs);
}

bool Version::operator!=(Version& rhs)
{
	return !IsEqual(rhs);
}
