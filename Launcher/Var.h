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

#include "targetver.h"

// Build & Publish mode
#define BUILD_NETCORE_RT_DEPENDENT		1
#define BUILD_NETCORE_SELF_CONTAINED	2
#define BUILD_NETFX						3
// Development only
#define BUILD_MODE		BUILD_NETCORE_RT_DEPENDENT

#ifdef PUBLISH_MODE
	// Force given build mode when publishing
	#undef BUILD_MODE
	#define BUILD_MODE PUBLISH_MODE
#endif

#if BUILD_MODE == BUILD_NETCORE_RT_DEPENDENT
	#undef CHECK_NETFX
	#define CHECK_NETCORE
#elif BUILD_MODE == BUILD_NETCORE_SELF_CONTAINED
	#undef CHECK_NETFX
	#undef CHECK_NETCORE
#elif BUILD_MODE == BUILD_NETFRAMEWORK
	#define CHECK_NETFX
	#undef CHECK_NETCORE
#else
	#error Invalid build mode BUILD_MODE, halting build.
#endif

// Enums
enum class ProcArch
{
	UNKNOWN = 0,
	X86,
	X64,
	ARM,
	ARM64,
};

enum class PEFormat
{
	UNKNOWN = 0,
	PE32 = 32,
	PE32_PLUS = 64,
};
