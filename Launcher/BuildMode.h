#pragma once

// Build & Publish mode
#define BUILD_NETFX						1
#define BUILD_NETCORE_RT_DEPENDENT		2
#define BUILD_NETCORE_SELF_CONTAINED	3
// Development only
#define BUILD_MODE		BUILD_NETCORE_RT_DEPENDENT

#ifdef PUBLISH_MODE
	// Force given build mode when publishing
#undef BUILD_MODE
#define BUILD_MODE PUBLISH_MODE
#endif
