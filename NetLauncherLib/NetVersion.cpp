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

#include "NetVersion.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlwapi.h>

// C++ Runtime Headers
#include <string>
#include <sstream>

// C Runtime Headers
#include <cstdint>

// Local Headers
#include "Helper.h"

NetVersion::NetVersion() :
	_major(0), _minor(0), _patch(0), _preview(0)
{ }

NetVersion::NetVersion(uint16_t major, uint16_t minor) :
	_major(major), _minor(minor), _patch(0), _preview(0)
{ }

NetVersion::NetVersion(uint16_t major, uint16_t minor, uint16_t patch) :
	_major(major), _minor(minor), _patch(patch), _preview(0)
{ }

NetVersion::NetVersion(uint16_t major, uint16_t minor, uint16_t patch, uint16_t preview) :
	_major(major), _minor(minor), _patch(patch), _preview(preview)
{ }

NetVersion::~NetVersion()
{
}

uint16_t NetVersion::getMajor() const
{
	return _major;
}

uint16_t NetVersion::getMinor() const
{
	return _minor;
}

uint16_t NetVersion::getPatch() const
{
	return _patch;
}

uint16_t NetVersion::getPreview() const
{
	return _preview;
}

std::wstring NetVersion::toStr(bool onlyMajorMinor) const
{
	// Version string is used for displaying purpose only
	std::wostringstream woss;
	woss << _major;
	woss << L'.';
	woss << _minor;
	if (onlyMajorMinor == false)
	{
		woss << L'.';
		woss << _patch;
		if (0 < _preview)
		{  // 6.0.0-preview.3
			woss << "-preview.";
			woss << _preview;
		}
	}
	return woss.str();
}

bool NetVersion::isEqual(const NetVersion& rhs, bool onlyMajorMinor) const
{
	bool isEqual = _major == rhs.getMajor() && _minor == rhs.getMinor();
	if (onlyMajorMinor == false)
	{
		isEqual &= _patch == rhs.getPatch();
		isEqual &= _preview == rhs.getPreview();
	}
	return isEqual;
}

// Is the target version compatible with this instance?
bool NetVersion::isCompatible(const NetVersion& rhs) const
{
	// https://learn.microsoft.com/en-us/dotnet/core/compatibility/categories
	// rhs needs to satisfy these conditions:
	// Major : equal 
	// Minor : equal
	// Patch : equal or higher (.NET Desktop Runtime often breaks forward compatibility even on patch version)
	if (_major != rhs.getMajor())
		return false;
	if (_minor != rhs.getMinor())
		return false;
	if (rhs.getPatch() < _patch)
		return false;

	// If lhs is a preview version, everything must be equal
	if (0 < _preview || 0 < rhs.getPreview())
		return isEqual(rhs, false);
	else
		return true;
}

bool NetVersion::parse(const std::string& str, NetVersion& ver)
{
	// Ex) 3.1.5
	// Ex) 6.0.0-preview.3.21201.3
	std::string slice;
	uint16_t major = 0;
	uint16_t minor = 0;
	uint16_t patch = 0;
	uint16_t preview = 0;

	static const std::string previewMark = "-preview.";
	std::string verStr;
	std::string labelStr;

	// Check if the string includes "preview" substring
	size_t dashPos = str.find(previewMark);
	if (dashPos != std::string::npos) // has "-preview.*"
	{
		verStr = str.substr(0, dashPos); // 6.0.0
		labelStr = str.substr(dashPos + previewMark.size()); // 3.21201.3
	}
	else
	{
		verStr = str; // 3.1.5
	}

	// Read major, minor and patch
	{
		const char* before = verStr.c_str();
		const char* after = before;

		// Read major
		// before=6.0.0
		after = Helper::tokenize(before, '.', slice);
		if (after == nullptr)
			return false;
		major = StrToIntA(slice.c_str());
		before = after;

		// Read patch (if exists)
		// before=0
		after = Helper::tokenize(before, '.', slice);
		if (after == nullptr)
		{ // Ex) 3.1
			minor = StrToIntA(slice.c_str());
		}
		else
		{ // Ex) 3.1.5
			minor = StrToIntA(slice.c_str());
			patch = StrToIntA(after);
		}
	}

	// Check preview major if labelStr is present
	if (0 < labelStr.size())
	{ // 3.21201.3
		const char* before = labelStr.c_str();
		const char* after = before;

		// We need only first number
		after = Helper::tokenize(before, '.', slice);
		if (after == nullptr) // No '.'? 
			preview = StrToIntA(before);
		else
			preview = StrToIntA(slice.c_str());
	}

	ver = NetVersion(major, minor, patch, preview);

	return true;
}

bool NetVersion::parse(const std::wstring& wstr, NetVersion& ver)
{
	std::wstring slice;
	uint16_t major = 0;
	uint16_t minor = 0;
	uint16_t patch = 0;
	uint16_t preview = 0;

	static const std::wstring previewMark = L"-preview.";
	std::wstring verStr;
	std::wstring labelStr;

	// Check if the string includes "preview" substring
	size_t dashPos = wstr.find(previewMark);
	if (dashPos != std::string::npos) // has "-preview.*"
	{
		verStr = wstr.substr(0, dashPos); // 6.0.0
		labelStr = wstr.substr(dashPos + previewMark.size()); // 3.21201.3
	}
	else
	{
		verStr = wstr; // 3.1.5
	}

	// Read major, minor and patch
	{
		const wchar_t* before = verStr.c_str();
		const wchar_t* after = before;

		// Read major
		after = Helper::tokenize(before, '.', slice);
		if (after == nullptr)
			return false;
		uint16_t major = StrToIntW(slice.c_str());
		before = after;

		// Read minor
		after = Helper::tokenize(before, '.', slice);
		if (after == nullptr)
			return false;
		uint16_t minor = StrToIntW(slice.c_str());
		before = after;

		// Read patch (if exists)
		after = Helper::tokenize(before, '.', slice);
		if (after == nullptr)
		{ // Ex) 3.1
		}
		else
		{ // Ex) 3.1.5
			patch = StrToIntW(slice.c_str());
		}
	}

	// Check preview major if labelStr is present
	if (0 < labelStr.size())
	{ // 3.21201.3
		const wchar_t* before = labelStr.c_str();
		const wchar_t* after = before;

		// We need only first number
		after = Helper::tokenize(before, '.', slice);
		if (after == nullptr) // No '.'? 
			preview = StrToIntW(before);
		else
			preview = StrToIntW(slice.c_str());
	}

	ver = NetVersion(major, minor, patch, preview);

	return true;
}

bool NetVersion::operator==(const NetVersion& rhs) const
{
	return isEqual(rhs);
}

bool NetVersion::operator!=(const NetVersion& rhs) const
{
	return !isEqual(rhs);
}

bool NetVersion::operator<(const NetVersion& rhs) const
{
	if (!(_major < rhs.getMajor()))
		return false;
	if (!(_minor < rhs.getMinor()))
		return false;
	if (!(_patch < rhs.getPatch()))
		return false;
	return true;
}

bool NetVersion::operator<=(const NetVersion& rhs) const
{
	if (!(_major <= rhs.getMajor()))
		return false;
	if (!(_minor <= rhs.getMinor()))
		return false;
	if (!(_patch <= rhs.getPatch()))
		return false;
	return true;
}

bool NetVersion::operator>(const NetVersion& rhs) const
{
	if (!(_major > rhs.getMajor()))
		return false;
	if (!(_minor > rhs.getMinor()))
		return false;
	if (!(_patch > rhs.getPatch()))
		return false;
	return true;
}

bool NetVersion::operator>=(const NetVersion& rhs) const
{
	if (!(_major >= rhs.getMajor()))
		return false;
	if (!(_minor >= rhs.getMinor()))
		return false;
	if (!(_patch >= rhs.getPatch()))
		return false;
	return true;
}
