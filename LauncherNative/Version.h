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

#pragma once

#include "Var.h"

// C++ Runtime Headers
#include <string>

// C Runtime Headers
#include <cstdint>

class Version
{
private:
	uint16_t _major;
	uint16_t _minor;
	uint16_t _patch;

public:
	// Constructors and Destructor
	Version();
	Version(uint16_t major, uint16_t minor);
	Version(uint16_t major, uint16_t minor, uint16_t patch);
	~Version();

	// Member Functions
	uint16_t GetMajor() const;
	uint16_t GetMinor() const;
	uint16_t GetPatch() const;
	std::wstring ToString(bool excludePatch = false);
	const std::wstring ToString(bool excludePatch = false) const;
	bool IsEqual(const Version& rhs, bool excludePatch = false) const;

	// Static Functions
	static bool Parse(const std::string& str, Version& ver);
	static bool Parse(const std::wstring& str, Version& ver);

	// Operator Overloading
	bool operator==(const Version& rhs) const;
	bool operator!=(const Version& rhs) const;
	bool operator<(const Version& rhs) const;
	bool operator<=(const Version& rhs) const;
	bool operator>(const Version& rhs) const;
	bool operator>=(const Version& rhs) const;
};
