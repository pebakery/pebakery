#include "CppUnitTest.h"

// C++ Runtime Headers
#include <string>
#include <vector>
#include <iostream>

// Local Headers
#include "NetDetector.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NetLauncherLibTests
{
	struct NetCoreDetectInfo
	{
	private:
		std::string _line;
		std::string _key;
		NetVersion _ver;
	public:
		std::string getLine() { return _line; }
		std::string getKey() { return _key; }
		NetVersion getVer() { return _ver; }

		NetCoreDetectInfo(const std::string& line, const std::string& key, NetVersion& ver)
		{
			_line = line;
			_key = key;
			_ver = ver;
		}
	};

	TEST_CLASS(NetCoreDetectorTests)
	{
	public:

		TEST_METHOD(ParseRuntimeInfoLineTest)
		{
			const std::string ASP_NET_CORE_ALL = "Microsoft.AspNetCore.All";
			const std::string ASP_NET_CORE_APP = "Microsoft.AspNetCore.App";
			const std::string NET_CORE_APP = "Microsoft.NETCore.App";
			const std::string WINDOWS_DESKTOP_APP = "Microsoft.WindowsDesktop.App";

			std::vector<NetCoreDetectInfo> infos;
			infos.push_back(NetCoreDetectInfo(
				"Microsoft.AspNetCore.All 2.1.27 [c:\\program files\\dotnet\\shared\\Microsoft.AspNetCore.All]",
				ASP_NET_CORE_ALL, NetVersion(2, 1, 27)));
			infos.push_back(NetCoreDetectInfo(
				"Microsoft.AspNetCore.App 2.1.27 [c:\\program files\\dotnet\\shared\\Microsoft.AspNetCore.App]",
				ASP_NET_CORE_APP, NetVersion(2, 1, 27)));
			infos.push_back(NetCoreDetectInfo(
				"Microsoft.AspNetCore.App 3.1.14 [c:\\program files\\dotnet\\shared\\Microsoft.AspNetCore.App]",
				ASP_NET_CORE_APP, NetVersion(3, 1, 14)));
			infos.push_back(NetCoreDetectInfo(
				"Microsoft.AspNetCore.App 5.0.5 [c:\\program files\\dotnet\\shared\\Microsoft.AspNetCore.App]",
				ASP_NET_CORE_APP, NetVersion(5, 0, 5)));
			infos.push_back(NetCoreDetectInfo(
				"Microsoft.AspNetCore.App 6.0.0-preview.3.21201.13 [c:\\program files\\dotnet\\shared\\Microsoft.AspNetCore.App]",
				ASP_NET_CORE_APP, NetVersion(6, 0, 0, 3)));
			infos.push_back(NetCoreDetectInfo(
				"Microsoft.NETCore.App 2.1.27 [c:\\program files\\dotnet\\shared\\Microsoft.NETCore.App]",
				NET_CORE_APP, NetVersion(2, 1, 27)));
			infos.push_back(NetCoreDetectInfo(
				"Microsoft.NETCore.App 3.1.14 [c:\\program files\\dotnet\\shared\\Microsoft.NETCore.App]",
				NET_CORE_APP, NetVersion(3, 1, 14)));
			infos.push_back(NetCoreDetectInfo(
				"Microsoft.NETCore.App 5.0.5 [c:\\program files\\dotnet\\shared\\Microsoft.NETCore.App]",
				NET_CORE_APP, NetVersion(5, 0, 5)));
			infos.push_back(NetCoreDetectInfo(
				"Microsoft.NETCore.App 6.0.0-preview.3.21201.4 [c:\\program files\\dotnet\\shared\\Microsoft.NETCore.App]",
				NET_CORE_APP, NetVersion(6, 0, 0, 3)));
			infos.push_back(NetCoreDetectInfo(
				"Microsoft.WindowsDesktop.App 3.1.14 [c:\\program files\\dotnet\\shared\\Microsoft.WindowsDesktop.App]",
				WINDOWS_DESKTOP_APP, NetVersion(3, 1, 14)));
			infos.push_back(NetCoreDetectInfo(
				"Microsoft.WindowsDesktop.App 5.0.5 [c:\\program files\\dotnet\\shared\\Microsoft.WindowsDesktop.App]",
				WINDOWS_DESKTOP_APP, NetVersion(5, 0, 5)));
			infos.push_back(NetCoreDetectInfo(
				"Microsoft.WindowsDesktop.App 6.0.0-preview.3.21201.3 [c:\\program files\\dotnet\\shared\\Microsoft.WindowsDesktop.App]",
				WINDOWS_DESKTOP_APP, NetVersion(6, 0, 0, 3)));

			for (NetCoreDetectInfo& info : infos)
			{
				std::string key;
				NetVersion ver;
				Assert::IsTrue(NetCoreDetector::parseRuntimeInfoLine(info.getLine(), key, ver));

				std::cout << "Key=" << key << ", exp=" << info.getKey() << std::endl;
				std::wcout << L"Ver=" << ver.toStr() << L", exp=" << info.getVer().toStr() << std::endl;

				Assert::AreEqual(0, info.getKey().compare(key));
				Assert::IsTrue(info.getVer() == ver);
			}
		}
	};
}
