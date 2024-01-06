/*
	Copyright (C) 2016-2023 Hajin Jang
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

// Build & Publish mode
#define BUILD_NETFX						1
#define BUILD_NETCORE_RT_DEPENDENT		2
#define BUILD_NETCORE_SELF_CONTAINED	3

// Development only
#ifndef BUILD_MODE
	#define BUILD_MODE		BUILD_NETCORE_RT_DEPENDENT
#endif

// Default .NET Core Target version
#ifndef NETCORE_TARGET_VER_MAJOR
	#define NETCORE_TARGET_VER_MAJOR	6
#endif
#ifndef NETCORE_TARGET_VER_MINOR
	#define NETCORE_TARGET_VER_MINOR	0
#endif
#ifndef NETCORE_TARGET_VER_PATCH
	#define NETCORE_TARGET_VER_PATCH	14
#endif

// Default .NET Framework Target version
#ifndef NETFX_TARGET_VER_MAJOR
	#define NETFX_TARGET_VER_MAJOR	4
#endif
#ifndef NETFX_TARGET_VER_MINOR
	#define NETFX_TARGET_VER_MINOR	8
#endif
#ifndef NETFX_TARGET_VER_PATCH
	#define NETFX_TARGET_VER_PATCH	0
#endif

// Force given build mode when publishing
#ifdef PUBLISH_MODE
#undef BUILD_MODE
#define BUILD_MODE PUBLISH_MODE
#endif
