/*
    Copyright (C) 2018-present Hajin Jang
    Licensed under GPL 3.0
*/

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Helper;
using System;
using System.IO;

namespace PEBakery.Core.Tests.Command
{
    [TestClass]
    [TestCategory(nameof(Command))]
    [TestCategory(nameof(Commands.CommandJson))]
    public class CommandJsonTests
    {
        [TestMethod]
        public void JsonPhoenixCompatibility()
        {
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string jsonFile = Path.Combine(tempDir, "config.json");
                File.WriteAllText(jsonFile, @"{""JS_TASKBAR"":{""theme"":""light""},""Microsoft.PowerShell:ExecutionPolicy"":""RemoteSigned"",""Items"":[1,""two""]}");

                EngineTests.Eval(s, $@"JSONWrite,{jsonFile},JS_TASKBAR.theme,dark", CodeType.JSONWrite, ErrorCheck.Success);
                EngineTests.Eval(s, $@"JSONRead,{jsonFile},JS_TASKBAR.theme", CodeType.JSONRead, ErrorCheck.Success);
                Assert.AreEqual("dark", s.ReturnValue);

                EngineTests.Eval(s, $@"JSONWrite,{jsonFile},JS_TASKBAR.smallicon,true", CodeType.JSONWrite, ErrorCheck.Success);
                EngineTests.Eval(s, $@"JSONRead,{jsonFile},JS_TASKBAR.smallicon", CodeType.JSONRead, ErrorCheck.Success);
                Assert.AreEqual("true", s.ReturnValue);

                EngineTests.Eval(s, $@"JSONWrite,{jsonFile},Microsoft\.PowerShell:ExecutionPolicy,Bypass", CodeType.JSONWrite, ErrorCheck.Success);
                EngineTests.Eval(s, $@"JSONRead,{jsonFile},Microsoft\.PowerShell:ExecutionPolicy", CodeType.JSONRead, ErrorCheck.Success);
                Assert.AreEqual("Bypass", s.ReturnValue);

                EngineTests.Eval(s, $@"JSONDelete,{jsonFile},JS_TASKBAR.smallicon", CodeType.JSONDelete, ErrorCheck.Success);
                EngineTests.Eval(s, $@"JSONRead,{jsonFile},JS_TASKBAR.smallicon,NOERR", CodeType.JSONRead, ErrorCheck.Success);
                Assert.AreEqual(string.Empty, s.ReturnValue);

                EngineTests.Eval(s, $@"JSONPretty,{jsonFile}", CodeType.JSONPretty, ErrorCheck.Success);
                StringAssert.Contains(File.ReadAllText(jsonFile), Environment.NewLine);
                EngineTests.Eval(s, $@"JSONCompact,{jsonFile}", CodeType.JSONCompact, ErrorCheck.Success);
                Assert.IsFalse(File.ReadAllText(jsonFile).Contains(Environment.NewLine, StringComparison.Ordinal));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void JsonNativeHelpers()
        {
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string jsonFile = Path.Combine(tempDir, "config.json");
                File.WriteAllText(jsonFile, @"{""Name"":""App"",""Items"":[1,""two"",true],""Obj"":{""B"":2,""A"":1}}");

                EngineTests.Eval(s, $@"JSONValidate,{jsonFile},%Valid%", CodeType.JSONValidate, ErrorCheck.Success);
                Assert.AreEqual("True", s.Variables["Valid"]);

                EngineTests.Eval(s, $@"JSONType,{jsonFile},Items,%Type%", CodeType.JSONType, ErrorCheck.Success);
                Assert.AreEqual("Array", s.Variables["Type"]);

                EngineTests.Eval(s, $@"JSONCount,{jsonFile},Items,%Count%", CodeType.JSONCount, ErrorCheck.Success);
                Assert.AreEqual("3", s.Variables["Count"]);

                EngineTests.Eval(s, $@"JSONReadArray,{jsonFile},Items,%List%", CodeType.JSONReadArray, ErrorCheck.Success);
                Assert.AreEqual("1|two|true", s.Variables["List"]);

                EngineTests.Eval(s, $@"JSONReadKeys,{jsonFile},Obj,%Keys%", CodeType.JSONReadKeys, ErrorCheck.Success);
                Assert.AreEqual("B|A", s.Variables["Keys"]);

                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Obj.A,%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("1", s.Variables["Value"]);

                EngineTests.Eval(s, $@"JSONFormat,{jsonFile},Pretty,SortKeys", CodeType.JSONFormat, ErrorCheck.Success);
                string formatted = File.ReadAllText(jsonFile);
                Assert.IsTrue(formatted.IndexOf(@"""A""", StringComparison.Ordinal) < formatted.IndexOf(@"""B""", StringComparison.Ordinal));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
