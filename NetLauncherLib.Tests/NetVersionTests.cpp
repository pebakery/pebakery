#include "CppUnitTest.h"

// C++ Runtime Headers
#include <string>
#include <vector>
#include <iostream>

// Local Headers
#include "NetVersion.h"
#include "Helper.h"

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

	template<typename T>
	struct NetVersionParseInfo
	{
	private:
		T _targetStr;
		NetVersion _expect;
	public:
		T getTargetStr() { return _targetStr; }
		NetVersion getExpectVer() { return _expect; }

		NetVersionParseInfo(const T& targetStr, const NetVersion& expect)
		{
			_targetStr = targetStr;
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

		TEST_METHOD(ParseTest_stringParse)
		{
			NetVersion targetVer(6, 0, 0, 3);

			std::vector<NetVersionParseInfo<std::string>> infos;
			infos.push_back(NetVersionParseInfo<std::string>("3.0.0", NetVersion(3, 0, 0)));
			infos.push_back(NetVersionParseInfo<std::string>("3.0", NetVersion(3, 0, 0)));
			infos.push_back(NetVersionParseInfo<std::string>("3.1.0", NetVersion(3, 1, 0)));
			infos.push_back(NetVersionParseInfo<std::string>("3.1", NetVersion(3, 1, 0)));
			infos.push_back(NetVersionParseInfo<std::string>("3.1.4", NetVersion(3, 1, 4)));
			infos.push_back(NetVersionParseInfo<std::string>("6.0.0-preview.3.21201.4", NetVersion(6, 0, 0, 3)));

			for (NetVersionParseInfo<std::string>& info : infos)
			{
				NetVersion ver;
				bool actual = NetVersion::parse(info.getTargetStr(), ver);
				Assert::IsTrue(actual);

				std::wostringstream woss;
				woss << L"Str=" << Helper::to_wstr(info.getTargetStr()) << L", exp=" << info.getExpectVer().toStr() << L", act=" << actual;
				Assert::IsTrue(info.getExpectVer().isEqual(ver), woss.str().c_str());
			}
		}

		TEST_METHOD(ParseTest_wstringParse)
		{
			NetVersion targetVer(6, 0, 0, 3);

			std::vector<NetVersionParseInfo<std::wstring>> infos;
			infos.push_back(NetVersionParseInfo<std::wstring>(L"3.0.0", NetVersion(3, 0, 0)));
			infos.push_back(NetVersionParseInfo<std::wstring>(L"3.0", NetVersion(3, 0, 0)));
			infos.push_back(NetVersionParseInfo<std::wstring>(L"3.1.0", NetVersion(3, 1, 0)));
			infos.push_back(NetVersionParseInfo<std::wstring>(L"3.1", NetVersion(3, 1, 0)));
			infos.push_back(NetVersionParseInfo<std::wstring>(L"3.1.4", NetVersion(3, 1, 4)));
			infos.push_back(NetVersionParseInfo<std::wstring>(L"6.0.0-preview.3.21201.4", NetVersion(6, 0, 0, 3)));

			for (NetVersionParseInfo<std::wstring>& info : infos)
			{
				NetVersion ver;
				bool actual = NetVersion::parse(info.getTargetStr(), ver);
				Assert::IsTrue(actual);

				std::wostringstream woss;
				woss << L"Str=" << info.getTargetStr() << L", exp=" << info.getExpectVer().toStr() << L", act=" << actual;
				Assert::IsTrue(info.getExpectVer().isEqual(ver), woss.str().c_str());
			}
		}

		TEST_METHOD(CompareTest_Lower)
		{
			auto testTemplate = [](const NetVersion& x, const NetVersion& y, bool expect)
			{
				return expect == (x < y);
			};

			testTemplate(NetVersion(6, 0, 12), NetVersion(3, 1, 0), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(5, 0, 0), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(6, 0, 0), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(6, 0, 12), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(6, 0, 14), true);
			testTemplate(NetVersion(6, 0, 12), NetVersion(7, 0, 0, 2), true);
			testTemplate(NetVersion(6, 0, 12), NetVersion(7, 0, 0), true);
			testTemplate(NetVersion(6, 0, 12), NetVersion(7, 0, 3), true);

			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0, 2), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0, 3), true);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0), true);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 5), true);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(7, 0, 0), true);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(7, 0, 3), true);
		}

		TEST_METHOD(CompareTest_LowerOrEqual)
		{
			auto testTemplate = [](const NetVersion& x, const NetVersion& y, bool expect)
			{
				return expect == (x <= y);
			};

			testTemplate(NetVersion(6, 0, 12), NetVersion(3, 1, 0), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(5, 0, 0), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(6, 0, 0), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(6, 0, 12), true);
			testTemplate(NetVersion(6, 0, 12), NetVersion(6, 0, 14), true);
			testTemplate(NetVersion(6, 0, 12), NetVersion(7, 0, 0, 2), true);
			testTemplate(NetVersion(6, 0, 12), NetVersion(7, 0, 0), true);
			testTemplate(NetVersion(6, 0, 12), NetVersion(7, 0, 3), true);

			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0, 2), true);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0, 3), true);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0), true);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 5), true);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(7, 0, 0), true);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(7, 0, 3), true);
		}

		TEST_METHOD(CompareTest_Equal)
		{
			auto testTemplate = [](const NetVersion& x, const NetVersion& y, bool expect)
			{
				return expect == (x < y);
			};

			testTemplate(NetVersion(6, 0, 12), NetVersion(3, 1, 0), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(5, 0, 0), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(6, 0, 0), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(6, 0, 12), true);
			testTemplate(NetVersion(6, 0, 12), NetVersion(6, 0, 14), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(7, 0, 0, 2), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(7, 0, 0), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(7, 0, 3), false);

			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0, 2), true);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0, 3), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 5), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(7, 0, 0), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(7, 0, 3), false);
		}

		TEST_METHOD(CompareTest_GreaterOrEqual)
		{
			auto testTemplate = [](const NetVersion& x, const NetVersion& y, bool expect)
			{
				return expect == (x >= y);
			};

			testTemplate(NetVersion(6, 0, 12), NetVersion(3, 1, 0), true);
			testTemplate(NetVersion(6, 0, 12), NetVersion(5, 0, 0), true);
			testTemplate(NetVersion(6, 0, 12), NetVersion(6, 0, 0), true);
			testTemplate(NetVersion(6, 0, 12), NetVersion(6, 0, 12), true);
			testTemplate(NetVersion(6, 0, 12), NetVersion(6, 0, 14), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(7, 0, 0, 2), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(7, 0, 0), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(7, 0, 3), false);

			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0), true);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0, 2), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0, 3), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 5), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(7, 0, 0), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(7, 0, 3), false);
		}

		TEST_METHOD(CompareTest_Greater)
		{
			auto testTemplate = [](const NetVersion& x, const NetVersion& y, bool expect)
			{
				return expect == (x > y);
			};

			testTemplate(NetVersion(6, 0, 12), NetVersion(3, 1, 0), true);
			testTemplate(NetVersion(6, 0, 12), NetVersion(5, 0, 0), true);
			testTemplate(NetVersion(6, 0, 12), NetVersion(6, 0, 0), true);
			testTemplate(NetVersion(6, 0, 12), NetVersion(6, 0, 12), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(6, 0, 14), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(7, 0, 0, 2), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(7, 0, 0), false);
			testTemplate(NetVersion(6, 0, 12), NetVersion(7, 0, 3), false);

			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0), true);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0, 2), true);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0, 3), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 0), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(6, 0, 5), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(7, 0, 0), false);
			testTemplate(NetVersion(6, 0, 0, 2), NetVersion(7, 0, 3), false);
		}
	};
}
