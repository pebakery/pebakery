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
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.XPath;

namespace PEBakery.Core.Commands
{
    public static class CommandXml
    {
        public static List<LogInfo> XMLRead(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_XMLRead info = (CodeInfo_XMLRead)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string xPath = StringEscaper.Preprocess(s, info.XPath);

            if (!TryLoadXml(logs, fileName, info.NoErr, out XDocument? doc, out XmlNamespaceManager? nsMgr))
            {
                s.ReturnValue = "1";
                logs.AddRange(SetDestVariable(s, info.DestVar, string.Empty));
                return logs;
            }

            if (!TryEvaluateXPathRaw(doc, xPath, nsMgr, out List<object> nodes, out string? xPathError))
            {
                s.ReturnValue = "2";
                logs.AddRange(SetDestVariable(s, info.DestVar, string.Empty));
                return LogInfo.LogErrorMessage(logs, xPathError!);
            }
            if (nodes.Count == 0)
            {
                s.ReturnValue = "2";
                LogState state = info.NoErr ? LogState.Ignore : LogState.Error;
                logs.Add(new LogInfo(state, $"XML XPath [{xPath}] did not match [{fileName}]", cmd));
                logs.AddRange(SetDestVariable(s, info.DestVar, string.Empty));
                return logs;
            }

            string value = XmlObjectToText(nodes[0]);
            s.ReturnValue = "0";
            logs.AddRange(SetDestVariable(s, info.DestVar, value));
            logs.Add(new LogInfo(LogState.Success, $"Read XML XPath [{xPath}] with value [{value}] from [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> XMLUpdate(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_XMLUpdate info = (CodeInfo_XMLUpdate)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string xPath = StringEscaper.Preprocess(s, info.XPath);
            string value = StringEscaper.Preprocess(s, info.Value);

            if (!TryLoadXml(logs, fileName, info.NoErr, out XDocument? doc, out XmlNamespaceManager? nsMgr))
            {
                s.ReturnValue = "1";
                return logs;
            }

            if (!TryEvaluateXPathNodeSet(doc, xPath, nsMgr, out List<object> nodes, out string? xPathError))
            {
                s.ReturnValue = "2";
                return LogInfo.LogErrorMessage(logs, xPathError!);
            }
            if (nodes.Count == 0)
            {
                s.ReturnValue = "2";
                LogState state = info.NoErr ? LogState.Ignore : LogState.Error;
                logs.Add(new LogInfo(state, $"XML XPath [{xPath}] did not find a match in [{fileName}]", cmd));
                return logs;
            }

            int updated;
            try
            {
                updated = nodes.Count(node => SetXmlObjectValue(node, value));
                SaveXml(fileName, doc, XmlFormatMode.Pretty);
            }
            catch (Exception e) when (e is IOException || e is XmlException || e is UnauthorizedAccessException)
            {
                s.ReturnValue = "1";
                return LogInfo.LogErrorMessage(logs, $"Unable to update XML XPath [{xPath}] in [{fileName}]: {Logger.LogExceptionMessage(e)}");
            }

            s.ReturnValue = "0";
            logs.Add(new LogInfo(LogState.Success, $"Updated [{updated}] node(s) at XML XPath [{xPath}] with value [{value}] in [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> XMLAdd(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_XMLAdd info = (CodeInfo_XMLAdd)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string xPath = StringEscaper.Preprocess(s, info.XPath);
            string name = StringEscaper.Preprocess(s, info.Name);
            string value = StringEscaper.Preprocess(s, info.Value);

            if (!TryLoadXml(logs, fileName, false, out XDocument? doc, out XmlNamespaceManager? nsMgr))
                return logs;

            if (!TryEvaluateXPathNodeSet(doc, xPath, nsMgr, out List<object> nodes, out string? xPathError))
                return LogInfo.LogErrorMessage(logs, xPathError!);
            if (nodes.Count == 0)
                return LogInfo.LogErrorMessage(logs, $"XML XPath [{xPath}] did not find a match in [{fileName}]");

            int added;
            try
            {
                added = nodes.Count(node => AddXmlNode(node, info.Operation, info.Type, name, value, nsMgr));
                SaveXml(fileName, doc, XmlFormatMode.Pretty);
            }
            catch (Exception e) when (e is IOException || e is XmlException || e is UnauthorizedAccessException)
            {
                return LogInfo.LogErrorMessage(logs, $"Unable to add XML node at XPath [{xPath}] in [{fileName}]: {Logger.LogExceptionMessage(e)}");
            }

            logs.Add(new LogInfo(LogState.Success, $"Added XML [{info.Type}] [{name}] to [{added}] node(s) in [{fileName}] at [{xPath}] with value [{value}]", cmd));
            return logs;
        }

        public static List<LogInfo> XMLDelete(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_XMLPath info = (CodeInfo_XMLPath)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string xPath = StringEscaper.Preprocess(s, info.XPath);

            if (!TryLoadXml(logs, fileName, false, out XDocument? doc, out XmlNamespaceManager? nsMgr))
                return logs;

            if (!TryEvaluateXPathNodeSet(doc, xPath, nsMgr, out List<object> nodes, out string? xPathError))
                return LogInfo.LogErrorMessage(logs, xPathError!);
            if (nodes.Count == 0)
                return LogInfo.LogErrorMessage(logs, $"XML XPath [{xPath}] did not find a match in [{fileName}]");

            int deleted;
            try
            {
                deleted = nodes.Count(RemoveXmlObject);
                SaveXml(fileName, doc, XmlFormatMode.Pretty);
            }
            catch (Exception e) when (e is IOException || e is XmlException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                return LogInfo.LogErrorMessage(logs, $"Unable to delete XML XPath [{xPath}] in [{fileName}]: {Logger.LogExceptionMessage(e)}");
            }

            logs.Add(new LogInfo(LogState.Success, $"Deleted [{deleted}] node(s) at XML XPath [{xPath}] from [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> XMLRename(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_XMLRename info = (CodeInfo_XMLRename)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string xPath = StringEscaper.Preprocess(s, info.XPath);
            string value = StringEscaper.Preprocess(s, info.Value);

            if (!TryLoadXml(logs, fileName, false, out XDocument? doc, out XmlNamespaceManager? nsMgr))
                return logs;

            if (!TryEvaluateXPathNodeSet(doc, xPath, nsMgr, out List<object> nodes, out string? xPathError))
                return LogInfo.LogErrorMessage(logs, xPathError!);
            if (nodes.Count == 0)
                return LogInfo.LogErrorMessage(logs, $"XML XPath [{xPath}] did not find a match in [{fileName}]");

            int renamed;
            try
            {
                renamed = nodes.Count(node => RenameXmlObject(node, value, nsMgr));
                SaveXml(fileName, doc, XmlFormatMode.Pretty);
            }
            catch (Exception e) when (e is IOException || e is XmlException || e is UnauthorizedAccessException)
            {
                return LogInfo.LogErrorMessage(logs, $"Unable to rename XML node at XPath [{xPath}] in [{fileName}]: {Logger.LogExceptionMessage(e)}");
            }

            logs.Add(new LogInfo(LogState.Success, $"Renamed [{renamed}] node(s) at XML XPath [{xPath}] in [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> XMLQuery(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_XMLQuery info = (CodeInfo_XMLQuery)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string xPath = StringEscaper.Preprocess(s, info.XPath);

            if (!TryLoadXml(logs, fileName, info.NoErr, out XDocument? doc, out XmlNamespaceManager? nsMgr))
            {
                s.ReturnValue = "1";
                logs.AddRange(SetDestVariable(s, info.DestVar, string.Empty));
                return logs;
            }

            if (!TryEvaluateXPathRaw(doc, xPath, nsMgr, out List<object> nodes, out string? xPathError))
            {
                s.ReturnValue = "2";
                logs.AddRange(SetDestVariable(s, info.DestVar, string.Empty));
                return LogInfo.LogErrorMessage(logs, xPathError!);
            }
            if (nodes.Count == 0)
            {
                s.ReturnValue = "2";
                LogState state = info.NoErr ? LogState.Ignore : LogState.Error;
                logs.Add(new LogInfo(state, $"XML XPath [{xPath}] did not find a match in [{fileName}]", cmd));
                logs.AddRange(SetDestVariable(s, info.DestVar, string.Empty));
                return logs;
            }

            string delim = info.Delim == null ? "|" : StringEscaper.Preprocess(s, info.Delim);
            string value = info.OutputMode == XmlQueryOutputMode.Xml
                ? string.Concat(nodes.Select(XmlObjectToXml))
                : string.Join(delim, nodes.Select(XmlObjectToText));
            s.ReturnValue = "0";
            logs.AddRange(SetDestVariable(s, info.DestVar, value));
            logs.Add(new LogInfo(LogState.Success, $"Queried XML XPath [{xPath}] with value [{value}] from [{fileName}]", cmd));
            return logs;
        }

        public static List<LogInfo> XMLFormat(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_XMLFormat info = (CodeInfo_XMLFormat)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            if (!TryLoadXml(logs, fileName, false, out XDocument? doc, out _))
                return logs;

            SaveXml(fileName, doc, info.Mode);
            logs.Add(new LogInfo(LogState.Success, $"Formatted XML file [{fileName}] as [{info.Mode}]", cmd));
            return logs;
        }

        public static List<LogInfo> XMLValidate(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_XMLValidate info = (CodeInfo_XMLValidate)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string? schemaFile = info.SchemaFile == null ? null : StringEscaper.Preprocess(s, info.SchemaFile);
            string? dtdFile = info.DtdFile == null ? null : StringEscaper.Preprocess(s, info.DtdFile);

            bool valid = ValidateXml(fileName, schemaFile, dtdFile, out string error);
            logs.AddRange(SetDestVariable(s, info.DestVar, valid ? "True" : "False"));
            if (valid)
            {
                logs.Add(new LogInfo(LogState.Success, $"XML file [{fileName}] is valid", cmd));
            }
            else
            {
                LogState state = info.NoErr ? LogState.Ignore : LogState.Error;
                logs.Add(new LogInfo(state, $"XML validation failed for [{fileName}]: {error}", cmd));
            }
            return logs;
        }

        public static List<LogInfo> XMLCount(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_XMLPathDest info = (CodeInfo_XMLPathDest)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string xPath = StringEscaper.Preprocess(s, info.XPath);

            if (!TryLoadXml(logs, fileName, false, out XDocument? doc, out XmlNamespaceManager? nsMgr))
                return logs;

            if (!TryEvaluateXPathRaw(doc, xPath, nsMgr, out List<object> nodes, out string? xPathError))
                return LogInfo.LogErrorMessage(logs, xPathError!);

            int count = nodes.Count;
            logs.AddRange(SetDestVariable(s, info.DestVar, count.ToString(CultureInfo.InvariantCulture)));
            logs.Add(new LogInfo(LogState.Success, $"XML XPath [{xPath}] count is [{count}]", cmd));
            return logs;
        }

        public static List<LogInfo> XMLReadList(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();
            CodeInfo_XMLPathDest info = (CodeInfo_XMLPathDest)cmd.Info;

            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string xPath = StringEscaper.Preprocess(s, info.XPath);
            string delim = info.Delim == null ? "|" : StringEscaper.Preprocess(s, info.Delim);

            if (!TryLoadXml(logs, fileName, false, out XDocument? doc, out XmlNamespaceManager? nsMgr))
                return logs;

            if (!TryEvaluateXPathRaw(doc, xPath, nsMgr, out List<object> nodes, out string? xPathError))
                return LogInfo.LogErrorMessage(logs, xPathError!);

            string value = string.Join(delim, nodes.Select(XmlObjectToText));
            logs.AddRange(SetDestVariable(s, info.DestVar, value));
            logs.Add(new LogInfo(LogState.Success, $"Read XML list [{xPath}] from [{fileName}]", cmd));
            return logs;
        }

        private static bool TryLoadXml(List<LogInfo> logs, string fileName, bool noErr, out XDocument doc, out XmlNamespaceManager nsMgr)
        {
            doc = new XDocument();
            nsMgr = new XmlNamespaceManager(new NameTable());

            if (!StringEscaper.PathSecurityCheck(fileName, out string errorMsg))
            {
                LogState state = noErr ? LogState.Ignore : LogState.Error;
                logs.Add(new LogInfo(state, errorMsg));
                return false;
            }
            if (!File.Exists(fileName))
            {
                LogState state = noErr ? LogState.Ignore : LogState.Error;
                logs.Add(new LogInfo(state, $"XML file [{fileName}] does not exist"));
                return false;
            }

            try
            {
                doc = XDocument.Load(fileName, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
                nsMgr = CreateNamespaceManager(doc);
                return true;
            }
            catch (Exception e) when (e is IOException || e is XmlException || e is UnauthorizedAccessException)
            {
                LogState state = noErr ? LogState.Ignore : LogState.Error;
                logs.Add(new LogInfo(state, $"Unable to read XML file [{fileName}]: {Logger.LogExceptionMessage(e)}"));
                return false;
            }
        }

        private static XmlNamespaceManager CreateNamespaceManager(XDocument doc)
        {
            XmlNamespaceManager nsMgr = new XmlNamespaceManager(new NameTable());
            if (doc.Root == null)
                return nsMgr;

            foreach (XAttribute attr in doc.Descendants().Attributes().Where(x => x.IsNamespaceDeclaration))
            {
                string prefix = attr.Name.LocalName == "xmlns" ? "_" : attr.Name.LocalName;
                if (!nsMgr.HasNamespace(prefix))
                    nsMgr.AddNamespace(prefix, attr.Value);
            }

            XNamespace defaultNs = doc.Root.GetDefaultNamespace();
            if (!string.IsNullOrEmpty(defaultNs.NamespaceName) && !nsMgr.HasNamespace("_"))
                nsMgr.AddNamespace("_", defaultNs.NamespaceName);

            foreach (XAttribute attr in doc.Descendants().Attributes())
            {
                // We already handled mapping the default namespace to '_' so this prevents
                // a 'Prefix "xmlns" is reserved for use by XML' ArgumentException.
                if (attr.IsNamespaceDeclaration)  
                    continue;

                if (attr.Name.NamespaceName.Length != 0)
                {
                    string prefix = attr.Parent?.GetPrefixOfNamespace(attr.Name.Namespace) ?? string.Empty;
                    // Only register non-empty, non-reserved prefixes that haven't been seen yet —
                    // "xml" and "xmlns" are reserved by the XML Namespaces spec and will throw an
                    // ArgumentException if passed to AddNamespace.
                    if (prefix.Length != 0 && prefix != "xml" && prefix != "xmlns" && !nsMgr.HasNamespace(prefix)) 
                        nsMgr.AddNamespace(prefix, attr.Name.NamespaceName);
                }
            }
            return nsMgr;
        }

        // Shared evaluation step that turns a malformed XPath (invalid syntax, bad axis, etc.) into
        // a reportable error instead of an unhandled XPathException. This is a user error,
        // not "the path legitimately matched nothing" - NoErr only exists to tolerate a valid-but-absent path.
        private static bool TryEvaluateXPathRaw(XDocument doc, string xPath, XmlNamespaceManager nsMgr, out List<object> nodes, out string? error)
        {
            object result;
            try
            {
                result = doc.XPathEvaluate(xPath, nsMgr);
            }
            catch (XPathException e)
            {
                nodes = new List<object>();
                error = $"Invalid XML XPath [{xPath}]: {Logger.LogExceptionMessage(e)}";
                return false;
            }

            nodes = result is IEnumerable<object> enumerable ? enumerable.ToList() : new List<object> { result };
            error = null;
            return true;
        }

        // Node-set-only evaluation for commands that mutate matched nodes (Delete/Update/Add/Rename).
        // Beyond catching malformed XPath (via TryEvaluateXPathRaw), this also rejects XPath
        // expressions that evaluate to a scalar (e.g. count(...), boolean(...), string(...)): a
        // scalar is not a node the mutation switches below know how to act on, and treating it as
        // "matched" would let these commands silently do nothing while still reporting Success.
        // Like a syntax error, this is a user error so not covered by NoErr.
        private static bool TryEvaluateXPathNodeSet(XDocument doc, string xPath, XmlNamespaceManager nsMgr, out List<object> nodes, out string? error)
        {
            if (!TryEvaluateXPathRaw(doc, xPath, nsMgr, out List<object> rawNodes, out error))
            {
                nodes = new List<object>();
                return false;
            }

            if (rawNodes.Count == 1 && rawNodes[0] is not XObject)
            {
                nodes = new List<object>();
                error = $"XML XPath [{xPath}] evaluated to a {rawNodes[0].GetType().Name.ToLowerInvariant()} value, not a set of XML nodes";
                return false;
            }

            nodes = rawNodes;
            error = null;
            return true;
        }

        private static string XmlObjectToText(object node)
        {
            return node switch
            {
                XElement e => e.Value,
                XAttribute a => a.Value,
                XCData c => c.Value,
                XText t => t.Value,
                XProcessingInstruction pi => pi.Data,
                XComment c => c.Value,
                _ => Convert.ToString(node, CultureInfo.InvariantCulture) ?? string.Empty,
            };
        }

        private static string XmlObjectToXml(object node)
        {
            return node switch
            {
                XElement e => e.ToString(SaveOptions.DisableFormatting),
                XAttribute a => $"{a.Name}=\"{a.Value}\"",
                XCData c => c.ToString(SaveOptions.DisableFormatting),
                XText t => t.Value,
                _ => Convert.ToString(node, CultureInfo.InvariantCulture) ?? string.Empty,
            };
        }

        private static bool SetXmlObjectValue(object node, string value)
        {
            switch (node)
            {
                case XElement e:
                    e.Value = value;
                    return true;
                case XAttribute a:
                    a.Value = value;
                    return true;
                case XCData c:
                    c.Value = value;
                    return true;
                case XText t:
                    t.Value = value;
                    return true;
                default:
                    return false;
            }
        }

        private static bool AddXmlNode(object node, XmlAddOperation operation, XmlAddType type, string name, string value, XmlNamespaceManager nsMgr)
        {
            if (type == XmlAddType.Attribute)
            {
                if (node is XElement e)
                {
                    e.SetAttributeValue(ResolveXName(name, nsMgr), value);
                    return true;
                }
                return false;
            }

            object newNode = type == XmlAddType.Text ? new XText(value) : new XElement(ResolveXName(name, nsMgr), value);
            if (operation == XmlAddOperation.Insert)
            {
                if (node is XElement e)
                {
                    e.AddFirst(newNode);
                    return true;
                }
                if (node is XNode xNode)
                {
                    xNode.AddBeforeSelf(newNode);
                    return true;
                }
            }
            else if (operation == XmlAddOperation.Append)
            {
                if (node is XElement e)
                {
                    e.Add(newNode);
                    return true;
                }
                if (node is XNode xNode)
                {
                    xNode.AddAfterSelf(newNode);
                    return true;
                }
            }
            else if (operation == XmlAddOperation.Subnode && node is XElement elem)
            {
                elem.Add(newNode);
                return true;
            }
            return false;
        }

        private static XName ResolveXName(string name, XmlNamespaceManager nsMgr)
        {
            int colon = name.IndexOf(':');
            if (colon < 0)
                return name; // no prefix, return as-is

            string prefix = name[..colon];
            string localName = name[(colon + 1)..];    
            
            string? uri = nsMgr.LookupNamespace(prefix); // Look up the given prefix

            return uri != null
                ? XName.Get(localName, uri)  // properly namespaced
                : name;                      // unknown prefix, fall through as-is
        }

        private static bool RemoveXmlObject(object node)
        {
            switch (node)
            {
                // Guard against calling Remove() on a node that's already detached (e.g. because it
                // was nested under another match that was removed earlier in the same batch, or the
                // same node somehow appears twice) - XNode/XAttribute.Remove() throws
                // InvalidOperationException if there's no parent/document to remove it from.
                case XElement e when e.Parent != null || e.Document != null:
                    e.Remove();
                    return true;
                case XAttribute a when a.Parent != null:
                    a.Remove();
                    return true;
                case XNode n when n.Parent != null || n.Document != null:
                    n.Remove();
                    return true;
                default:
                    return false;
            }
        }

        private static bool RenameXmlObject(object node, string value, XmlNamespaceManager nsMgr)
        {
            switch (node)
            {
                case XElement e:
                    XName newElementName = ResolveXName(value, nsMgr);
                    e.Name = newElementName;
                    return true;
                case XAttribute a:
                    XElement? parent = a.Parent;
                    if (parent != null)
                    {
                        string attrValue = a.Value;
                        a.Remove();
                        parent.SetAttributeValue(ResolveXName(value, nsMgr), attrValue);
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private static bool ValidateXml(string fileName, string? schemaFile, string? dtdFile, out string error)
        {
            error = string.Empty;
            try
            {
                XmlReaderSettings settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Parse,
                    XmlResolver = null,
                };

                if (schemaFile != null)
                {
                    settings.ValidationType = ValidationType.Schema;
                    settings.Schemas.Add(null, schemaFile);
                }
                else if (dtdFile != null)
                {
                    if (!File.Exists(dtdFile))
                    {
                        error = $"DTD file [{dtdFile}] does not exist";
                        return false;
                    }
                    settings.ValidationType = ValidationType.DTD;
                    settings.XmlResolver = new XmlUrlResolver();
                }

                string validationError = string.Empty;
                settings.ValidationEventHandler += (_, e) =>
                {
                    if (e.Severity == XmlSeverityType.Error)
                        validationError = e.Message;
                };

                using XmlReader reader = XmlReader.Create(fileName, settings);
                while (reader.Read())
                {
                }
                error = validationError;
                return error.Length == 0;
            }
            catch (Exception e) when (e is IOException || e is XmlException || e is XmlSchemaException || e is UnauthorizedAccessException)
            {
                error = Logger.LogExceptionMessage(e);
                return false;
            }
        }

        private static void SaveXml(string fileName, XDocument doc, XmlFormatMode mode)
        {
            XmlWriterSettings writerSettings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false), // UTF-8 without BOM
                Indent = mode != XmlFormatMode.Compact,
                OmitXmlDeclaration = false,
            };

            using XmlWriter writer = XmlWriter.Create(fileName, writerSettings);
            doc.Save(writer);
        }

        private static List<LogInfo> SetDestVariable(EngineState s, string destVar, string value)
        {
            string escapedValue = StringEscaper.Escape(value, false, true);
            return Variables.SetVariable(s, destVar, escapedValue, false, false, false);
        }
    }
}
