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
        #region JsonPhoenixCompatibility
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
                EngineTests.Eval(s, $@"JSONRead,{jsonFile},JS_TASKBAR.theme,%Value%", CodeType.JSONRead, ErrorCheck.Success);
                Assert.AreEqual("dark", s.Variables["Value"]);

                EngineTests.Eval(s, $@"JSONWrite,{jsonFile},JS_TASKBAR.smallicon,true", CodeType.JSONWrite, ErrorCheck.Success);
                EngineTests.Eval(s, $@"JSONRead,{jsonFile},JS_TASKBAR.smallicon,%Value%", CodeType.JSONRead, ErrorCheck.Success);
                Assert.AreEqual("true", s.Variables["Value"]);

                EngineTests.Eval(s, $@"JSONWrite,{jsonFile},Microsoft\.PowerShell:ExecutionPolicy,Bypass", CodeType.JSONWrite, ErrorCheck.Success);
                EngineTests.Eval(s, $@"JSONRead,{jsonFile},Microsoft\.PowerShell:ExecutionPolicy,%Value%", CodeType.JSONRead, ErrorCheck.Success);
                Assert.AreEqual("Bypass", s.Variables["Value"]);

                EngineTests.Eval(s, $@"JSONDelete,{jsonFile},JS_TASKBAR.smallicon", CodeType.JSONDelete, ErrorCheck.Success);
                EngineTests.Eval(s, $@"JSONRead,{jsonFile},JS_TASKBAR.smallicon,%Value%,NOERR", CodeType.JSONRead, ErrorCheck.Success);
                Assert.AreEqual(string.Empty, s.Variables["Value"]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion
        #region JsonNativehelpers
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

                EngineTests.Eval(s, $@"JSONReadArray,{jsonFile},Items,%List%,DELIM=;", CodeType.JSONReadArray, ErrorCheck.Success);
                Assert.AreEqual("1;two;true", s.Variables["List"]);

                EngineTests.Eval(s, $@"JSONReadKeys,{jsonFile},Obj,%Keys%", CodeType.JSONReadKeys, ErrorCheck.Success);
                Assert.AreEqual("B|A", s.Variables["Keys"]);

                EngineTests.Eval(s, $@"JSONReadKeys,{jsonFile},Obj,%Keys%,DELIM=#$s", CodeType.JSONReadKeys, ErrorCheck.Success);
                Assert.AreEqual("B A", s.Variables["Keys"]);

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
        #endregion
        #region JSONQuery — single-node backward-compat and OutputMode
        /// <summary>
        /// Paths without wildcards must still use the OutputMode switch.
        /// Also validates integer-index paths are not broken by the new wildcard
        /// detection in ParseJsonSegment.
        /// </summary>
        [TestMethod]
        public void JsonQuerySingleNodeCompat()
        {
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string jsonFile = Path.Combine(tempDir, "data.json");
                File.WriteAllText(jsonFile, @"{""Name"":""App"",""Count"":42,""Active"":true,""Obj"":{""A"":1,""B"":2},""Items"":[""zero"",""one"",""two""]}");

                // Default (raw) mode: string value returned without surrounding quotes
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Name,%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("App", s.Variables["Value"]);
                Assert.AreEqual("0", s.ReturnValue);

                // Default mode: numeric value
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Count,%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("42", s.Variables["Value"]);

                // Default mode: boolean value
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Active,%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("true", s.Variables["Value"]);

                // Compact mode: object returns compact JSON
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Obj,%Value%,Compact", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual(@"{#$qA#$q:1,#$qB#$q:2}", s.Variables["Value"]);

                // Json (pretty) mode: object returns indented JSON
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Obj,%Value%,Json", CodeType.JSONQuery, ErrorCheck.Success);
                string pretty = s.Variables["Value"];
                Assert.IsTrue(pretty.Contains("#$qA#$q"), "Json mode output should contain key A");
                Assert.IsTrue(pretty.Length > @"{""A"":1,""B"":2}".Length, "Json mode output should be longer than compact");

                // Regression: integer-index path not confused for wildcard after ParseJsonSegment change
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Items[2],%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("two", s.Variables["Value"]);

                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Items[0],%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("zero", s.Variables["Value"]);

                // NOERR on missing path: empty result, ReturnValue=2, no assertion failure
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Missing,%Value%,NOERR", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual(string.Empty, s.Variables["Value"]);
                Assert.AreEqual("2", s.ReturnValue);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion
        #region JSONQuery — Wildcards
        /// <summary>
        /// tags[*] and items[*].property cover the most common real-world wildcard shapes.
        /// </summary>
        [TestMethod]
        public void JsonQueryWildcardScalars()
        {
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string jsonFile = Path.Combine(tempDir, "data.json");

                // String array - raw values joined with the default delimiter
                File.WriteAllText(jsonFile, @"{""Tags"":[""alpha"",""beta"",""gamma""]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Tags[*],%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("alpha|beta|gamma", s.Variables["Value"]);
                Assert.AreEqual("0", s.ReturnValue);

                // Number array
                File.WriteAllText(jsonFile, @"{""Scores"":[10,20,30]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Scores[*],%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("10|20|30", s.Variables["Value"]);

                // Boolean array
                File.WriteAllText(jsonFile, @"{""Flags"":[true,false,true]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Flags[*],%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("true|false|true", s.Variables["Value"]);

                // Mixed scalar types (number, string, bool) in one array
                File.WriteAllText(jsonFile, @"{""Mixed"":[1,""two"",true]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Mixed[*],%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("1|two|true", s.Variables["Value"]);

                // Single-element array — wildcard mode active but no delimiter in output
                File.WriteAllText(jsonFile, @"{""Items"":[""only""]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Items[*],%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("only", s.Variables["Value"]);

                // Null element in array - empty string for that slot (consistent with JSONReadArray)
                File.WriteAllText(jsonFile, @"{""Items"":[1,null,3]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Items[*],%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("1||3", s.Variables["Value"]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Drill-down pattern: Items[*].Property extracts a scalar from every object in an array.
        /// Elements that lack the target property are silently skipped.
        /// </summary>
        [TestMethod]
        public void JsonQueryWildcardDrillDown()
        {
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string jsonFile = Path.Combine(tempDir, "data.json");

                // Every element has the property
                File.WriteAllText(jsonFile, @"{""Items"":[{""Name"":""Alice""},{""Name"":""Bob""},{""Name"":""Carol""}]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Items[*].Name,%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("Alice|Bob|Carol", s.Variables["Value"]);

                // Numeric property values
                File.WriteAllText(jsonFile, @"{""Items"":[{""Score"":10},{""Score"":20},{""Score"":30}]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Items[*].Score,%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("10|20|30", s.Variables["Value"]);

                // Some elements are missing the property → those slots produce no output (not empty strings)
                File.WriteAllText(jsonFile, @"{""Items"":[{""Name"":""A""},{""Other"":""X""},{""Name"":""B""}]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Items[*].Name,%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("A|B", s.Variables["Value"]);

                // Element type mismatch (scalar in array, not an object) → also skipped silently
                File.WriteAllText(jsonFile, @"{""Items"":[{""Name"":""A""},42,{""Name"":""B""}]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Items[*].Name,%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("A|B", s.Variables["Value"]);

                // Two-level deep property
                File.WriteAllText(jsonFile, @"{""Users"":[{""Profile"":{""City"":""Seattle""}},{""Profile"":{""City"":""Portland""}}]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Users[*].Profile.City,%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("Seattle|Portland", s.Variables["Value"]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Bare * wildcard expands all values of an object, and wildcards can be chained
        /// across two levels to flatten nested structures.
        /// </summary>
        [TestMethod]
        public void JsonQueryWildcardObject()
        {
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string jsonFile = Path.Combine(tempDir, "data.json");

                // Bare * on an object key - all values of that sub-object
                File.WriteAllText(jsonFile, @"{""Env"":{""OS"":""Windows"",""Arch"":""x64""}}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Env.*,%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("Windows|x64", s.Variables["Value"]);

                // Chained wildcards: array of arrays - flatten two levels of nesting
                // Matrix[*] expands the outer array; .* expands each inner array
                File.WriteAllText(jsonFile, @"{""Matrix"":[[1,2],[3,4]]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Matrix[*].*,%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("1|2|3|4", s.Variables["Value"]);

                // Wildcard across array of objects then object values
                // Items[*] yields each object; .* yields all values of each object
                File.WriteAllText(jsonFile, @"{""Items"":[{""X"":1,""Y"":2},{""X"":3,""Y"":4}]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Items[*].*,%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("1|2|3|4", s.Variables["Value"]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Complex nodes (objects and arrays) produced by a wildcard expansion are
        /// serialised as compact JSON rather than attempting to flatten them further.
        /// </summary>
        [TestMethod]
        public void JsonQueryWildcardComplexResults()
        {
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string jsonFile = Path.Combine(tempDir, "data.json");

                // Array of objects - each object serialised as compact JSON
                File.WriteAllText(jsonFile, @"{""Items"":[{""X"":1},{""Y"":2}]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Items[*],%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                // Double-quotes are stored internally escaped as #$q
                Assert.AreEqual(@"{#$qX#$q:1}|{#$qY#$q:2}", s.Variables["Value"]);

                // Array of arrays - each inner array serialised as compact JSON
                File.WriteAllText(jsonFile, @"{""Rows"":[[1,2],[3,4]]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Rows[*],%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("[1,2]|[3,4]", s.Variables["Value"]);

                // Mixed scalar and complex: scalar uses raw string, object uses compact JSON
                File.WriteAllText(jsonFile, @"{""Items"":[42,{""nested"":true}]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Items[*],%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual(@"42|{#$qnested#$q:true}", s.Variables["Value"]);

                // Complex result still uses pretty JSON when OutputMode=Json is set
                File.WriteAllText(jsonFile, @"{""Items"":[{""A"":1}]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Items[*],%Value%,Json", CodeType.JSONQuery, ErrorCheck.Success);
                string prettyResult = s.Variables["Value"];
                Assert.IsTrue(prettyResult.Contains("#$qA#$q"));
                Assert.IsTrue(prettyResult.Length > @"{""A"":1}".Length, "Json output mode should produce indented JSON for complex nodes");
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Wildcard queries that produce zero results set ReturnValue=2, just like a
        /// missing path in single-node mode.
        /// </summary>
        [TestMethod]
        public void JsonQueryWildcardNoMatch()
        {
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string jsonFile = Path.Combine(tempDir, "data.json");

                // [*] on an empty array - zero results
                File.WriteAllText(jsonFile, @"{""Items"":[]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Items[*],%Value%,NOERR", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual(string.Empty, s.Variables["Value"]);
                Assert.AreEqual("2", s.ReturnValue);

                // [*] on a non-existent key - zero results
                File.WriteAllText(jsonFile, @"{""Other"":""value""}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Missing[*],%Value%,NOERR", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual(string.Empty, s.Variables["Value"]);
                Assert.AreEqual("2", s.ReturnValue);

                // .* wildcard on a scalar value - nothing to expand
                File.WriteAllText(jsonFile, @"{""Count"":42}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Count.*,%Value%,NOERR", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual(string.Empty, s.Variables["Value"]);
                Assert.AreEqual("2", s.ReturnValue);

                // Drill-down where NO array elements have the target property
                File.WriteAllText(jsonFile, @"{""Items"":[{""X"":1},{""X"":2}]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Items[*].Name,%Value%,NOERR", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual(string.Empty, s.Variables["Value"]);
                Assert.AreEqual("2", s.ReturnValue);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion
        #region JSONQuery — multi-path (comma-separated filter)
        /// <summary>
        /// A filter containing commas selects multiple independent paths and merges
        /// their results. The comma must be quoted in script syntax ("A,B") so PEBakery
        /// does not split it into separate arguments.
        /// Missing paths are silently skipped — no error is raised for the partial hit.
        /// </summary>
        [TestMethod]
        public void JsonQueryMultiPath()
        {
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string jsonFile = Path.Combine(tempDir, "data.json");
                File.WriteAllText(jsonFile, @"{""Name"":""MyApp"",""Version"":""1.0.0"",""Build"":42}");

                // Two top-level scalar properties
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},""Name,Version"",%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("MyApp|1.0.0", s.Variables["Value"]);
                Assert.AreEqual("0", s.ReturnValue);

                // Three properties — order matches declaration order in the filter
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},""Name,Build,Version"",%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("MyApp|42|1.0.0", s.Variables["Value"]);

                // One present, one absent - only the found value appears; no error raised
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},""Name,Missing"",%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("MyApp", s.Variables["Value"]);
                Assert.AreEqual("0", s.ReturnValue);

                // All paths absent - ReturnValue=2 (NOERR suppresses the error log)
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},""Missing1,Missing2"",%Value%,NOERR", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual(string.Empty, s.Variables["Value"]);
                Assert.AreEqual("2", s.ReturnValue);

                // Multi-path can mix flat properties with wildcard sub-paths
                File.WriteAllText(jsonFile, @"{""Name"":""App"",""Items"":[{""Name"":""Alice""},{""Name"":""Bob""}]}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},""Name,Items[*].Name"",%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("App|Alice|Bob", s.Variables["Value"]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion
        #region JSONQuery - delimiters
        /// <summary>
        /// The DELIM option overrides the default '|' separator, the same as JSONReadArray and JSONReadKeys.
        /// </summary>
        [TestMethod]
        public void JsonQueryMultiResultDelimiter()
        {
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string jsonFile = Path.Combine(tempDir, "data.json");
                File.WriteAllText(jsonFile, @"{""Tags"":[""alpha"",""beta"",""gamma""]}");

                // Default delimiter is '|'
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Tags[*],%Value%", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("alpha|beta|gamma", s.Variables["Value"]);

                // Semicolon delimiter
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Tags[*],%Value%,DELIM=;", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("alpha;beta;gamma", s.Variables["Value"]);

                // Comma delimiter (useful when the consumer is CSV-aware)
                // Note: Comma with Delim= must be wraped in double-quotes or escaped as #$c or it will be interpreted as an a new (empty) argument.
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Tags[*],%Value%,""DELIM=,""", CodeType.JSONQuery, ErrorCheck.Success); 
                Assert.AreEqual("alpha,beta,gamma", s.Variables["Value"]);

                // Space delimiter
                // Note: Space with Delim= must be wraped in double-quotes or escaped as #$s or it will be stripped.
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},Tags[*],%Value%,""DELIM= """, CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("alpha beta gamma", s.Variables["Value"]);

                // Multi-path also uses the configured delimiter
                File.WriteAllText(jsonFile, @"{""A"":""1"",""B"":""2"",""C"":""3""}");
                EngineTests.Eval(s, $@"JSONQuery,{jsonFile},""A,B,C"",%Value%,DELIM=-", CodeType.JSONQuery, ErrorCheck.Success);
                Assert.AreEqual("1-2-3", s.Variables["Value"]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion
        #region JSONValidate
        /// <summary>
        /// JSONValidate should reject malformed JSON and succeed on JSONC (comments and trailing commas).
        /// </summary>
        [TestMethod]
        public void JsonValidateEdgeCases()
        {
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string jsonFile = Path.Combine(tempDir, "data.json");

                // Invalid JSON - %Valid% = False
                File.WriteAllText(jsonFile, "{ not valid json }");
                EngineTests.Eval(s, $@"JSONValidate,{jsonFile},%Valid%,NOERR", CodeType.JSONValidate, ErrorCheck.Success);
                Assert.AreEqual("False", s.Variables["Valid"]);

                // JSONC (comments + trailing comma) - valid in relaxed mode
                File.WriteAllText(jsonFile, "{ /* comment */ \"key\": \"value\", }");
                EngineTests.Eval(s, $@"JSONValidate,{jsonFile},%Valid%", CodeType.JSONValidate, ErrorCheck.Success);
                Assert.AreEqual("True", s.Variables["Valid"]);

                // Strict mode must reject JSONC
                EngineTests.Eval(s, $@"JSONValidate,{jsonFile},%Valid%,Strict,NOERR", CodeType.JSONValidate, ErrorCheck.Success);
                Assert.AreEqual("False", s.Variables["Valid"]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion
        #region JSONCount and Format Compact
        /// <summary>
        /// JSONCount on scalars (returns 1) and missing paths (returns -1).
        /// JSONFormat Compact mode.
        /// </summary>
        [TestMethod]
        public void JsonCountAndFormatCompact()
        {
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string jsonFile = Path.Combine(tempDir, "data.json");
                File.WriteAllText(jsonFile, @"{""Name"":""App"",""Items"":[1,2,3]}");

                // Scalar - count is 1
                EngineTests.Eval(s, $@"JSONCount,{jsonFile},Name,%Count%", CodeType.JSONCount, ErrorCheck.Success);
                Assert.AreEqual("1", s.Variables["Count"]);

                // Missing path - count is -1
                EngineTests.Eval(s, $@"JSONCount,{jsonFile},Missing,%Count%", CodeType.JSONCount, ErrorCheck.Success);
                Assert.AreEqual("-1", s.Variables["Count"]);

                // JSONFormat Compact round-trips without whitespace
                File.WriteAllText(jsonFile, "{\n  \"A\": 1,\n  \"B\": 2\n}");
                EngineTests.Eval(s, $@"JSONFormat,{jsonFile},Compact", CodeType.JSONFormat, ErrorCheck.Success);
                string compact = File.ReadAllText(jsonFile);
                Assert.IsFalse(compact.Contains('\n'), "Compact mode should remove newlines");
                Assert.IsTrue(compact.Contains(@"""A"":1"), "Compact mode should preserve data");
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        #endregion
        }
    }
}
