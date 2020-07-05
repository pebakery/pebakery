#pragma once

#include "targetver.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
// #include <WinDef.h>
#include <Windows.h>

// Check if .NET is installed on the system
// #define CHECK_NETFX
#define CHECK_NETCORE

// Constants
constexpr size_t MAX_PATH_LONG = 32768;
constexpr size_t BUFSIZE = 256;

constexpr const WCHAR* ERR_MSG_UNABLE_TO_GET_ABSPATH = L"Unable to query absolute path of PEBakeryLauncher.exe";
constexpr const WCHAR* ERR_MSG_UNABLE_TO_FIND_BINARY = L"Unable to find PEBakery.";
constexpr const WCHAR* ERR_MSG_UNABLE_TO_LAUNCH_BINARY = L"Unable to launch PEBakery.";
