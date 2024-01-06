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

// C++ Runtime Headers
#include <string>

// C Runtime Headers
#include <cstdint>

// Represent .NET Core versions
class NetVersion
{
private:
	uint16_t _major;
	uint16_t _minor;
	uint16_t _patch;
	uint16_t _preview; // Ex) 6.0.0-preview.3

public:
	// Constructors and Destructor
	NetVersion();
	NetVersion(uint16_t major, uint16_t minor);
	NetVersion(uint16_t major, uint16_t minor, uint16_t patch);
	NetVersion(uint16_t major, uint16_t minor, uint16_t patch, uint16_t preview);
	~NetVersion();

	// Member Functions
	uint16_t getMajor() const;
	uint16_t getMinor() const;
	uint16_t getPatch() const;
	uint16_t getPreview() const;
	std::wstring toStr(bool onlyMajorMinor = false) const;
	bool isEqual(const NetVersion& rhs, bool onlyMajorMinor = false) const;
	bool isCompatible(const NetVersion& rhs) const;
	void clear();

	// Static Functions
	static bool parse(const std::string& str, NetVersion& ver);
	static bool parse(const std::wstring& str, NetVersion& ver);

	// Operator Overloading
	bool operator==(const NetVersion& rhs) const;
	bool operator!=(const NetVersion& rhs) const;
	bool operator<(const NetVersion& rhs) const;
	bool operator<=(const NetVersion& rhs) const;
	bool operator>(const NetVersion& rhs) const;
	bool operator>=(const NetVersion& rhs) const;
};
