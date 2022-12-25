#include "CppUnitTest.h"

// C++ Runtime Headers
#include <string>
#include <vector>
#include <iostream>

// Local Headers
#include "NetVersion.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NetLauncherLibTests
{
	struct NetVersionCheckInfo
	{
	private:
		NetVersion _ver;
		bool _expect;
	public:
		NetVersion getVer() { return _ver; }
		bool getExpect() { return _expect; }

		NetVersionCheckInfo(const NetVersion& ver, bool expect)
		{
			_ver = ver;
			_expect = expect;
		}
	};

	TEST_CLASS(NetVersionTests)
	{
	public:

		TEST_METHOD(IsCompatibleTest_NormalToAll)
		{
			NetVersion targetVer(6, 1, 3);

			std::vector<NetVersionCheckInfo> infos;
			infos.push_back(NetVersionCheckInfo(NetVersion(3, 0, 0), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(3, 1, 0), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 0, 0, 2), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 0, 0), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 1, 0), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 1, 1), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 1, 3), true));
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 1, 7), true));
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 2, 0), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(7, 0, 0), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(7, 1, 3), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(7, 2, 0), false));

			for (NetVersionCheckInfo& info : infos)
			{
				bool actual = targetVer.isCompatible(info.getVer());

				std::wostringstream woss;
				woss << L"Ver=" << info.getVer().toStr() << L", exp=" << info.getExpect() << L", act=" << actual;
				Assert::AreEqual(actual, info.getExpect(), woss.str().c_str());
			}
		}

		TEST_METHOD(IsCompatibleTest_NormalToPreview)
		{
			NetVersion targetVer(6, 0, 0);

			std::vector<NetVersionCheckInfo> infos;
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 0, 0, 2), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 0, 0, 3), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 0, 0, 4), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 0, 0), true));
			infos.push_back(NetVersionCheckInfo(NetVersion(7, 0, 0), false));

			for (NetVersionCheckInfo& info : infos)
			{
				bool actual = targetVer.isCompatible(info.getVer());

				std::wostringstream woss;
				woss << L"Ver=" << info.getVer().toStr() << L", exp=" << info.getExpect() << L", act=" << actual;
				Assert::AreEqual(actual, info.getExpect(), woss.str().c_str());
			}
		}

		TEST_METHOD(IsCompatibleTest_PreviewToAll)
		{
			NetVersion targetVer(6, 0, 0, 3);

			std::vector<NetVersionCheckInfo> infos;
			infos.push_back(NetVersionCheckInfo(NetVersion(3, 0, 0), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(3, 1, 0), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 0, 0, 2), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 0, 0, 3), true));
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 0, 0, 4), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 0, 0), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(6, 1, 0), false));
			infos.push_back(NetVersionCheckInfo(NetVersion(7, 0, 0), false));

			for (NetVersionCheckInfo& info : infos)
			{
				bool actual = targetVer.isCompatible(info.getVer());

				std::wostringstream woss;
				woss << L"Ver=" << info.getVer().toStr() << L", exp=" << info.getExpect() << L", act=" << actual;
				Assert::AreEqual(actual, info.getExpect(), woss.str().c_str());
			}
		}
	};
}
