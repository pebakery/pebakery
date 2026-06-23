/*
    Copyright (C) 2016-present Hajin Jang
    Licensed under GPL 3.0
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PEBakery.Core.Commands
{
    public static class CommandJson
    {
        private static readonly JsonSerializerOptions JsonPrettyOptions = new JsonSerializerOptions { WriteIndented = true };
        private static readonly JsonSerializerOptions JsonCompactOptions = new JsonSerializerOptions { WriteIndented = false };

        /// <summary>
        /// Read options that tolerate JSONC input (// line comments, /* block comments */, trailing commas).
        /// Comments and trailing commas are silently discarded on any subsequent write/format operation,
        /// producing standards-compliant JSON output.
        /// </summary>
        private static readonly JsonDocumentOptions JsoncReadOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static List<LogInfo> JSONRead(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_JSONRead info = (CodeInfo_JSONRead)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string path = NormalizeJsonFilter(StringEscaper.Preprocess(s, info.Path));

            if (!TryLoadJson(logs, fileName, info.NoErr, out JsonNode? root))
            {
                s.ReturnValue = "1";
                logs.AddRange(SetDestVariable(s, info.DestVar, string.Empty));
                return logs;
            }

            JsonNode? node = SelectJsonNode(root, path);
            if (node == null)
            {
                s.ReturnValue = "2";
                LogState state = info.NoErr ? LogState.Ignore : LogState.Error;
                logs.Add(new LogInfo(state, $"JSON path [{path}] does not exist in [{fileName}]", cmd));
                logs.AddRange(SetDestVariable(s, info.DestVar, string.Empty));
                return logs;
            }

            string value = JsonNodeToRawString(node);
            s.ReturnValue = "0";
            logs.AddRange(SetDestVariable(s, info.DestVar, value));
            logs.Add(new LogInfo(LogState.Success, $"Read JSON path [{path}] with value [{value}] from [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> JSONWrite(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_JSONWrite info = (CodeInfo_JSONWrite)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string path = NormalizeJsonFilter(StringEscaper.Preprocess(s, info.Path));
            string value = StringEscaper.Preprocess(s, info.Value);

            if (!TryLoadJson(logs, fileName, false, out JsonNode? root))
                return logs;
            if (path.Length == 0)
                return LogInfo.LogErrorMessage(logs, "JSON path cannot be empty");

            JsonNode valueNode = ParseJsonValue(value);
            if (!SetJsonNode(ref root, path, valueNode))
                return LogInfo.LogErrorMessage(logs, $"Unable to write JSON value [{value}] to path [{path}] in [{fileName}]");

            WriteJson(fileName, root, JsonCompactOptions);
            logs.Add(new LogInfo(LogState.Success, $"Wrote JSON value [{value}] to path [{path}] in [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> JSONDelete(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_JSONDelete info = (CodeInfo_JSONDelete)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string path = NormalizeJsonFilter(StringEscaper.Preprocess(s, info.Path));

            if (!TryLoadJson(logs, fileName, false, out JsonNode? root))
                return logs;
            if (path.Length == 0)
                return LogInfo.LogErrorMessage(logs, "JSON path cannot be empty");
            if (!DeleteJsonNode(root, path))
                return LogInfo.LogErrorMessage(logs, $"JSON path [{path}] does not exist in [{fileName}]");

            WriteJson(fileName, root, JsonCompactOptions);
            logs.Add(new LogInfo(LogState.Success, $"Deleted JSON path [{path}] from [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> JSONFormat(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_JSONFormat info = (CodeInfo_JSONFormat)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            if (!TryLoadJson(logs, fileName, false, out JsonNode? root))
                return logs;

            if (info.SortKeys && root != null)
                root = SortJsonKeys(root);

            JsonSerializerOptions options = info.Mode == JsonFormatMode.Pretty ? JsonPrettyOptions : JsonCompactOptions;
            WriteJson(fileName, root, options);
            logs.Add(new LogInfo(LogState.Success, $"Formatted JSON file [{fileName}] as [{info.Mode}]", cmd));
            return logs;
        }

        public static List<LogInfo> JSONQuery(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_JSONQuery info = (CodeInfo_JSONQuery)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string filter = NormalizeJsonFilter(StringEscaper.Preprocess(s, info.Filter));
            string delim = info.Delim == null ? "|" : StringEscaper.Preprocess(s, info.Delim);

            if (!TryLoadJson(logs, fileName, info.NoErr, out JsonNode? root))
            {
                s.ReturnValue = "1";
                logs.AddRange(SetDestVariable(s, info.DestVar, string.Empty));
                return logs;
            }

            // Decide whether the filter triggers multi-result mode:
            //   - comma-separated paths  — each path is evaluated independently, results are merged.
            //   - wildcard segments ([*] or *)  — each wildcard fans out across array elements / object values.
            // When neither applies single-node behaviour is preserved.
            List<string> paths = SplitMultiPath(filter);
            bool isMultiMode = paths.Count > 1 || paths.Any(p => ParseJsonPath(p).Any(part => part.IsWildcard));

            List<JsonNode?> results = [];
            foreach (string p in paths)
                results.AddRange(SelectJsonNodes(root, p));

            if (results.Count == 0)
            {
                s.ReturnValue = "2";
                LogState state = info.NoErr ? LogState.Ignore : LogState.Error;
                logs.Add(new LogInfo(state, $"JSON filter [{filter}] did not find a match in [{fileName}]", cmd));
                logs.AddRange(SetDestVariable(s, info.DestVar, string.Empty));
                return logs;
            }

            string value;
            if (!isMultiMode)
            {
                // Single path without wildcards.
                JsonNode? node = results[0];
                if (node == null)
                {
                    s.ReturnValue = "2";
                    LogState state = info.NoErr ? LogState.Ignore : LogState.Error;
                    logs.Add(new LogInfo(state, $"JSON filter [{filter}] did not find a match in [{fileName}]", cmd));
                    logs.AddRange(SetDestVariable(s, info.DestVar, string.Empty));
                    return logs;
                }
                value = info.OutputMode switch
                {
                    JsonQueryOutputMode.Json => node.ToJsonString(JsonPrettyOptions),
                    JsonQueryOutputMode.Compact => node.ToJsonString(JsonCompactOptions),
                    _ => JsonNodeToRawString(node),
                };
                s.ReturnValue = "0";
                logs.AddRange(SetDestVariable(s, info.DestVar, value));
                logs.Add(new LogInfo(LogState.Success, $"Queried JSON value [{value}] from [{fileName}] using filter [{filter}] ", cmd));
            }
            else
            {
                // Multi-result mode:
                // - Scalar values are output as raw strings. 
                // - complex nodes (objects / arrays) fall back to compact JSON to play it safe.
                value = string.Join(delim, results.Select(n => FormatNodeForJoin(n, info.OutputMode)));
                s.ReturnValue = "0";
                logs.AddRange(SetDestVariable(s, info.DestVar, value));
                logs.Add(new LogInfo(LogState.Success, $"Queried [{results.Count}] JSON value(s) from [{fileName}] using filter [{filter}]", cmd));
            }

            return logs;
        }

        public static List<LogInfo> JSONValidate(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_JSONValidate info = (CodeInfo_JSONValidate)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            bool valid = true;
            try
            {
                // Validate as Strict standards compliant JSON (default keyword) or allow JSONC extensions
                JsonDocumentOptions parseOptions = info.Strict ? default : JsoncReadOptions;

                using FileStream fs = File.OpenRead(fileName);
                using JsonDocument doc = JsonDocument.Parse(fs, parseOptions);
            }
            catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException)
            {
                valid = false;
                LogState state = info.NoErr ? LogState.Ignore : LogState.Error;
                logs.Add(new LogInfo(state, $"JSON validation failed for [{fileName}]: {Logger.LogExceptionMessage(e)}", cmd));
            }

            logs.AddRange(SetDestVariable(s, info.DestVar, valid ? "True" : "False"));
            if (valid)
                logs.Add(new LogInfo(LogState.Success, $"JSON file [{fileName}] is valid {(info.Strict ? "(Strict)" : "")}", cmd));
            return logs;
        }

        public static List<LogInfo> JSONType(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_JSONPathDest info = (CodeInfo_JSONPathDest)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string path = NormalizeJsonFilter(StringEscaper.Preprocess(s, info.Path));

            if (!TryLoadJson(logs, fileName, false, out JsonNode? root))
                return logs;

            JsonNode? node = SelectJsonNode(root, path);
            string typeName = node switch
            {
                null => "Missing",
                JsonObject => "Object",
                JsonArray => "Array",
                JsonValue value => JsonValueTypeName(value),
                _ => "Unknown",
            };

            logs.AddRange(SetDestVariable(s, info.DestVar, typeName));
            logs.Add(new LogInfo(LogState.Success, $"JSON path [{path}] type is [{typeName}]", cmd));
            return logs;
        }

        public static List<LogInfo> JSONCount(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_JSONPathDest info = (CodeInfo_JSONPathDest)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string path = NormalizeJsonFilter(StringEscaper.Preprocess(s, info.Path));

            if (!TryLoadJson(logs, fileName, false, out JsonNode? root))
                return logs;

            JsonNode? node = SelectJsonNode(root, path);
            int count = node switch
            {
                JsonObject obj => obj.Count,
                JsonArray arr => arr.Count,
                null => -1,
                _ => 1,
            };

            logs.AddRange(SetDestVariable(s, info.DestVar, count.ToString(CultureInfo.InvariantCulture)));
            logs.Add(new LogInfo(LogState.Success, $"JSON path [{path}] count is [{count}]", cmd));
            return logs;
        }

        public static List<LogInfo> JSONReadArray(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_JSONPathDest info = (CodeInfo_JSONPathDest)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string path = NormalizeJsonFilter(StringEscaper.Preprocess(s, info.Path));
            string delim = info.Delim == null ? "|" : StringEscaper.Preprocess(s, info.Delim);

            if (!TryLoadJson(logs, fileName, false, out JsonNode? root))
                return logs;

            if (SelectJsonNode(root, path) is not JsonArray arr)
                return LogInfo.LogErrorMessage(logs, $"JSON path [{path}] is not an array");

            string value = string.Join(delim, arr.Select(x => x == null ? string.Empty : JsonNodeToRawString(x)));
            logs.AddRange(SetDestVariable(s, info.DestVar, value));
            logs.Add(new LogInfo(LogState.Success, $"Read JSON array [{path}] with value [{value}] from [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> JSONReadKeys(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_JSONPathDest info = (CodeInfo_JSONPathDest)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string path = NormalizeJsonFilter(StringEscaper.Preprocess(s, info.Path));
            string delim = info.Delim == null ? "|" : StringEscaper.Preprocess(s, info.Delim);

            if (!TryLoadJson(logs, fileName, false, out JsonNode? root))
                return logs;

            if (SelectJsonNode(root, path) is not JsonObject obj)
                return LogInfo.LogErrorMessage(logs, $"JSON path [{path}] is not an object");

            logs.AddRange(SetDestVariable(s, info.DestVar, string.Join(delim, obj.Select(x => x.Key))));
            logs.Add(new LogInfo(LogState.Success, $"Read JSON object keys [{path}] from [{fileName}]", cmd));
            return logs;
        }

        private static bool TryLoadJson(List<LogInfo> logs, string fileName, bool noErr, out JsonNode? root)
        {
            root = null;
            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
            {
                LogState state = noErr ? LogState.Ignore : LogState.Error;
                logs.Add(new LogInfo(state, errorMsg));
                return false;
            }
            if (!File.Exists(fileName))
            {
                LogState state = noErr ? LogState.Ignore : LogState.Error;
                logs.Add(new LogInfo(state, $"JSON file [{fileName}] does not exist"));
                return false;
            }

            try
            {
                root = JsonNode.Parse(File.ReadAllText(fileName, Encoding.UTF8),
                    nodeOptions: null, documentOptions: JsoncReadOptions);
                if (root == null)
                {
                    LogState state = noErr ? LogState.Ignore : LogState.Error;
                    logs.Add(new LogInfo(state, $"JSON file [{fileName}] is empty"));
                    return false;
                }
                return true;
            }
            catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException)
            {
                LogState state = noErr ? LogState.Ignore : LogState.Error;
                logs.Add(new LogInfo(state, $"Unable to read JSON file [{fileName}]: {Logger.LogExceptionMessage(e)}"));
                return false;
            }
        }

        private static string NormalizeJsonFilter(string filter)
        {
            if (filter.Equals(".", StringComparison.Ordinal))
                return string.Empty;
            return filter.StartsWith(".", StringComparison.Ordinal) ? filter[1..] : filter;
        }

        private static JsonNode? SelectJsonNode(JsonNode? root, string path)
        {
            if (root == null)
                return null;
            if (path.Length == 0)
                return root;

            JsonNode? node = root;
            foreach (JsonPathPart part in ParseJsonPath(path))
            {
                if (node == null)
                    return null;

                if (part.Property != null)
                {
                    if (node is not JsonObject obj)
                        return null;
                    node = obj[part.Property];
                }

                if (part.Index != null)
                {
                    if (node is not JsonArray arr || part.Index.Value < 0 || arr.Count <= part.Index.Value)
                        return null;
                    node = arr[part.Index.Value];
                }
            }
            return node;
        }

        /// <summary>
        /// Multi-Path version of SelectJsonNode.
        /// Supports wildcard segments ([*] / *) that fan out into multiple results.
        /// </summary>
        private static List<JsonNode?> SelectJsonNodes(JsonNode? root, string path)
        {
            if (root == null) return [];
            if (path.Length == 0) return [root];

            List<JsonNode?> current = [root];

            foreach (JsonPathPart part in ParseJsonPath(path))
            {
                List<JsonNode?> next = [];
                foreach (JsonNode? node in current)
                    ExpandPart(node, part, next);
                current = next;
            }

            return current;
        }

        /// <summary>
        /// Evaluates one JsonPathPart against node and appends every match to results.
        ///   - Named property - navigate into the object key (silently skips when absent).
        ///   - Numeric index  - selects one array element (skips when out of range).
        ///   - Wildcard       - fans out: all array elements or all object values.
        /// </summary>
        private static void ExpandPart(JsonNode? node, JsonPathPart part, List<JsonNode?> results)
        {
            if (node == null) return;

            JsonNode? target = node;

            // Navigate to the named property when specified.
            if (part.Property != null)
            {
                if (target is not JsonObject obj) return;
                if (!obj.ContainsKey(part.Property)) return; // key absent - no match
                target = obj[part.Property];
            }

            // Apply index, wildcard expansion, or accept the resolved node as-is.
            if (part.IsWildcard)
            {
                // [*]  -  all elements of an array
                // *    -  all values of an object  (or all elements if target happens to be an array)
                if (target is JsonArray arr)
                    results.AddRange(arr);
                else if (target is JsonObject objWild)
                    foreach (KeyValuePair<string, JsonNode?> kv in objWild)
                        results.Add(kv.Value);
                // Wildcard on a scalar or null intentional produces no output.
            }
            else if (part.Index != null)
            {
                if (target is not JsonArray arr || part.Index.Value < 0 || arr.Count <= part.Index.Value)
                    return;
                results.Add(arr[part.Index.Value]);
            }
            else
            {
                results.Add(target);
            }
        }

        /// <summary>
        /// Splits a top-level comma-separated list of JSON paths while respecting bracket depth,
        /// so that commas inside […] segments are never treated as path separators.
        /// </summary>
        /// <example>
        /// "name,age"           - ["name", "age"]
        /// "items[0],items[1]"  - ["items[0]", "items[1]"]
        /// "items[*].name"      - ["items[*].name"] (single path, no split)
        /// </example>
        private static List<string> SplitMultiPath(string filter)
        {
            List<string> paths = [];
            StringBuilder sb = new StringBuilder();
            int depth = 0;

            foreach (char c in filter)
            {
                if (c == '[') depth++;
                else if (c == ']') depth--;
                else if (c == ',' && depth == 0)
                {
                    string segment = sb.ToString().Trim();
                    if (segment.Length > 0) paths.Add(segment);
                    sb.Clear();
                    continue;
                }
                sb.Append(c);
            }

            string last = sb.ToString().Trim();
            if (last.Length > 0) paths.Add(last);

            // Guarantee at least one entry (the original string) so we never get an empty list.
            return paths.Count > 0 ? paths : [filter];
        }

        /// <summary>
        /// Formats a single node for inclusion in a delimited multi-result string.
        /// Scalar values are returned as raw strings (no surrounding quotes).
        /// Complex nodes (objects and arrays) are serialised as compact JSON 
        /// because it would be ambiguous to flatten them further.
        /// </summary>
        private static string FormatNodeForJoin(JsonNode? node, JsonQueryOutputMode outputMode)
        {
            if (node == null) return string.Empty;
            if (node is JsonValue) return JsonNodeToRawString(node);
            // Object or array: output as JSON. Pretty-print only when explicitly requested.
            return node.ToJsonString(outputMode == JsonQueryOutputMode.Json ? JsonPrettyOptions : JsonCompactOptions);
        }

        private static bool SetJsonNode(ref JsonNode? root, string path, JsonNode value)
        {
            List<JsonPathPart> parts = ParseJsonPath(path);
            if (parts.Count == 0)
            {
                root = value;
                return true;
            }

            if (root == null)
                root = new JsonObject();

            JsonNode? node = root;
            for (int i = 0; i < parts.Count - 1; i++)
            {
                JsonPathPart part = parts[i];
                JsonPathPart next = parts[i + 1];

                if (part.Property != null)
                {
                    if (node is not JsonObject obj)
                        return false;
                    JsonNode? child = obj[part.Property];
                    if (child == null)
                    {
                        child = next.Property == null && next.Index != null ? new JsonArray() : new JsonObject();
                        obj[part.Property] = child;
                    }
                    node = child;
                }

                if (part.Index != null)
                {
                    if (node is not JsonArray arr)
                        return false;
                    EnsureJsonArraySize(arr, part.Index.Value + 1);
                    JsonNode? child = arr[part.Index.Value];
                    if (child == null)
                    {
                        child = next.Property == null && next.Index != null ? new JsonArray() : new JsonObject();
                        arr[part.Index.Value] = child;
                    }
                    node = child;
                }
            }

            JsonPathPart last = parts[^1];
            if (last.Property != null)
            {
                if (node is not JsonObject obj)
                    return false;
                if (last.Index == null)
                {
                    obj[last.Property] = value;
                    return true;
                }

                JsonNode? child = obj[last.Property];
                if (child == null)
                {
                    child = new JsonArray();
                    obj[last.Property] = child;
                }
                if (child is not JsonArray arr)
                    return false;
                EnsureJsonArraySize(arr, last.Index.Value + 1);
                arr[last.Index.Value] = value;
                return true;
            }

            if (last.Index != null && node is JsonArray lastArr)
            {
                EnsureJsonArraySize(lastArr, last.Index.Value + 1);
                lastArr[last.Index.Value] = value;
                return true;
            }

            return false;
        }

        private static bool DeleteJsonNode(JsonNode? root, string path)
        {
            List<JsonPathPart> parts = ParseJsonPath(path);
            if (root == null || parts.Count == 0)
                return false;

            JsonNode? node = root;
            for (int i = 0; i < parts.Count - 1; i++)
            {
                JsonPathPart part = parts[i];
                node = SelectJsonPart(node, part);
                if (node == null)
                    return false;
            }

            JsonPathPart last = parts[^1];
            if (last.Property != null)
            {
                if (node is not JsonObject obj || !obj.ContainsKey(last.Property))
                    return false;
                if (last.Index == null)
                    return obj.Remove(last.Property);

                if (obj[last.Property] is not JsonArray arr || last.Index.Value < 0 || arr.Count <= last.Index.Value)
                    return false;
                arr.RemoveAt(last.Index.Value);
                return true;
            }

            if (last.Index != null && node is JsonArray lastArr && 0 <= last.Index.Value && last.Index.Value < lastArr.Count)
            {
                lastArr.RemoveAt(last.Index.Value);
                return true;
            }
            return false;
        }

        private static JsonNode? SelectJsonPart(JsonNode? node, JsonPathPart part)
        {
            if (part.Property != null)
            {
                if (node is not JsonObject obj)
                    return null;
                node = obj[part.Property];
            }
            if (part.Index != null)
            {
                if (node is not JsonArray arr || part.Index.Value < 0 || arr.Count <= part.Index.Value)
                    return null;
                node = arr[part.Index.Value];
            }
            return node;
        }

        private static List<JsonPathPart> ParseJsonPath(string path)
        {
            List<JsonPathPart> parts = new List<JsonPathPart>();
            StringBuilder b = new StringBuilder();
            bool escaped = false;

            void Flush()
            {
                string segment = b.ToString();
                b.Clear();
                if (segment.Length == 0)
                    return;
                parts.Add(ParseJsonSegment(segment));
            }

            foreach (char c in path)
            {
                if (escaped)
                {
                    b.Append(c);
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '.')
                {
                    Flush();
                }
                else
                {
                    b.Append(c);
                }
            }
            if (escaped)
                b.Append('\\');
            Flush();
            return parts;
        }

        private static JsonPathPart ParseJsonSegment(string segment)
        {
            string? property = segment;
            int? index = null;
            bool isWildcard = false;

            int bracketIdx = segment.IndexOf('[', StringComparison.Ordinal);
            if (bracketIdx != -1 && segment.EndsWith("]", StringComparison.Ordinal))
            {
                property = bracketIdx == 0 ? null : segment[..bracketIdx];
                string indexStr = segment[(bracketIdx + 1)..^1];
                if (indexStr == "*")
                    isWildcard = true;
                else if (int.TryParse(indexStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                    index = idx;
                // else: unrecognised bracket content — leave property set, index null
            }
            else if (segment == "*")
            {
                // Bare wildcard: expands all keys of an object or all elements of an array.
                property = null;
                isWildcard = true;
            }
            else if (int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numericIndex))
            {
                property = null;
                index = numericIndex;
            }

            return new JsonPathPart(property, index, isWildcard);
        }

        private static void EnsureJsonArraySize(JsonArray arr, int size)
        {
            while (arr.Count < size)
                arr.Add(null);
        }

        private static JsonNode ParseJsonValue(string value)
        {
            try
            {
                JsonNode? parsed = JsonNode.Parse(value);
                if (parsed != null)
                    return parsed;
            }
            catch (JsonException)
            {
            }
            return JsonValue.Create(value)!;
        }

        private static string JsonNodeToRawString(JsonNode node)
        {
            if (node is JsonValue value)
            {
                if (value.TryGetValue(out string? str))
                    return str;
                if (value.TryGetValue(out bool boolean))
                    return boolean ? "true" : "false";
                if (value.TryGetValue(out JsonElement element))
                    return element.ToString();
            }
            return node.ToJsonString(JsonCompactOptions);
        }

        private static string JsonValueTypeName(JsonValue value)
        {
            if (value.TryGetValue(out string? _))
                return "String";
            if (value.TryGetValue(out bool _))
                return "Bool";
            if (value.TryGetValue(out JsonElement element))
            {
                return element.ValueKind switch
                {
                    JsonValueKind.Number => "Number",
                    JsonValueKind.Null => "Null",
                    JsonValueKind.True => "Bool",
                    JsonValueKind.False => "Bool",
                    _ => element.ValueKind.ToString(),
                };
            }
            return "Value";
        }

        private static JsonNode SortJsonKeys(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                JsonObject sorted = new JsonObject();
                foreach (KeyValuePair<string, JsonNode?> pair in obj.OrderBy(x => x.Key, StringComparer.Ordinal))
                    sorted[pair.Key] = pair.Value == null ? null : SortJsonKeys(pair.Value);
                return sorted;
            }
            if (node is JsonArray arr)
            {
                JsonArray sorted = new JsonArray();
                foreach (JsonNode? item in arr)
                    sorted.Add(item == null ? null : SortJsonKeys(item));
                return sorted;
            }
            return node.DeepClone();
        }

        private static void WriteJson(string fileName, JsonNode? root, JsonSerializerOptions options)
        {
            File.WriteAllText(fileName, root?.ToJsonString(options) ?? "null", Encoding.UTF8);
        }

        private readonly record struct JsonPathPart(string? Property, int? Index, bool IsWildcard = false);

        private static List<LogInfo> SetDestVariable(EngineState s, string destVar, string value)
        {
            string escapedValue = StringEscaper.Escape(value, false, true);
            return Variables.SetVariable(s, destVar, escapedValue, false, false, false);
        }    
    }
}

