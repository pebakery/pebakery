/*
	Copyright (C) 2016-2020 Hajin Jang
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

#include "Version.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <strsafe.h>
#include <shlwapi.h>

// C++ Runtime Headers
#include <string>
#include <sstream>

// C Runtime Headers
#include <cstdint>

// Local Headers
#include "Helper.h"

using namespace std;

Version::Version() :
	_major(0), _minor(0), _patch(0)
{ }

Version::Version(uint16_t major, uint16_t minor) :
	_major(major), _minor(minor), _patch(0)
{ }

Version::Version(uint16_t major, uint16_t minor, uint16_t patch) :
	_major(major), _minor(minor), _patch(patch)
{ }

Version::~Version()
{
}

uint16_t Version::GetMajor() const
{
	return _major;
}

uint16_t Version::GetMinor() const
{
	return _minor;
}

uint16_t Version::GetPatch() const
{
	return _patch;
}

wstring Version::ToString(bool excludePatch)
{
	return static_cast<const Version>(*this).ToString(excludePatch);
}

const wstring Version::ToString(bool excludePatch) const
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

bool Version::IsEqual(const Version& rhs, bool excludePatch) const
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
	uint16_t major = StrToIntA(slice.c_str());

	// Read minor and patch
	const char* patchPtr = Helper::Tokenize(minorPtr, '.', slice);
	if (patchPtr == nullptr)
	{ // No patch, Ex) 3.1
		uint16_t minor = StrToIntA(minorPtr);
		ver = Version(major, minor);
	}
	else
	{ // Found patch, Ex) 3.1.5
		uint16_t minor = StrToIntA(slice.c_str());
		uint16_t patch = StrToIntA(patchPtr);
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
	uint16_t major = StrToIntW(slice.c_str());

	// Read minor
	after = Helper::Tokenize(before, '.', slice);
	if (after == nullptr)
		return false;
	uint16_t minor = StrToIntW(slice.c_str());

	// Read patch (if exists)
	after = Helper::Tokenize(before, '.', slice);
	if (after == nullptr)
	{ // Ex) 3.1
		ver = Version(major, minor);
	}
	else
	{ // Ex) 3.1.5
		uint16_t patch = StrToIntW(slice.c_str());
		ver = Version(major, minor, patch);
	}

	return true;
}

bool Version::operator==(const Version& rhs) const
{
	return IsEqual(rhs);
}

bool Version::operator!=(const Version& rhs) const
{
	return !IsEqual(rhs);
}

bool Version::operator<(const Version& rhs) const
{
	if (!(_major < rhs.GetMajor()))
		return false;
	if (!(_minor < rhs.GetMinor()))
		return false;
	if (!(_patch < rhs.GetPatch()))
		return false;
	return true;
}

bool Version::operator<=(const Version& rhs) const
{
	if (!(_major <= rhs.GetMajor()))
		return false;
	if (!(_minor <= rhs.GetMinor()))
		return false;
	if (!(_patch <= rhs.GetPatch()))
		return false;
	return true;
}

bool Version::operator>(const Version& rhs) const
{
	if (!(_major > rhs.GetMajor()))
		return false;
	if (!(_minor > rhs.GetMinor()))
		return false;
	if (!(_patch > rhs.GetPatch()))
		return false;
	return true;
}

bool Version::operator>=(const Version& rhs) const
{
	if (!(_major >= rhs.GetMajor()))
		return false;
	if (!(_minor >= rhs.GetMinor()))
		return false;
	if (!(_patch >= rhs.GetPatch()))
		return false;
	return true;
}
