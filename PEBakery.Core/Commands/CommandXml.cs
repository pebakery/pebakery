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

            List<object> nodes = EvaluateXPath(doc, xPath, nsMgr).ToList();
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

            List<object> nodes = EvaluateXPath(doc, xPath, nsMgr).ToList();
            if (nodes.Count == 0)
            {
                s.ReturnValue = "2";
                LogState state = info.NoErr ? LogState.Ignore : LogState.Error;
                logs.Add(new LogInfo(state, $"XML XPath [{xPath}] did not find a match in [{fileName}]", cmd));
                return logs;
            }

            foreach (object node in nodes)
                SetXmlObjectValue(node, value);

            SaveXml(fileName, doc, XmlFormatMode.Pretty);
            s.ReturnValue = "0";
            logs.Add(new LogInfo(LogState.Success, $"Updated XML XPath [{xPath}] with value [{value}] in [{fileName}]", cmd));
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

            List<object> nodes = EvaluateXPath(doc, xPath, nsMgr).ToList();
            if (nodes.Count == 0)
                return LogInfo.LogErrorMessage(logs, $"XML XPath [{xPath}] did not find a match in [{fileName}]");

            foreach (object node in nodes)
                AddXmlNode(node, info.Operation, info.Type, name, value, nsMgr);

            SaveXml(fileName, doc, XmlFormatMode.Pretty);
            logs.Add(new LogInfo(LogState.Success, $"Added XML [{info.Type}] [{name}] in [{fileName}] at [{xPath}] with value [{value}]", cmd));
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

            List<object> nodes = EvaluateXPath(doc, xPath, nsMgr).ToList();
            if (nodes.Count == 0)
                return LogInfo.LogErrorMessage(logs, $"XML XPath [{xPath}] did not find a match in [{fileName}]");

            foreach (object node in nodes)
                RemoveXmlObject(node);

            SaveXml(fileName, doc, XmlFormatMode.Pretty);
            logs.Add(new LogInfo(LogState.Success, $"Deleted XML XPath [{xPath}] from [{fileName}]", cmd));
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

            List<object> nodes = EvaluateXPath(doc, xPath, nsMgr).ToList();
            if (nodes.Count == 0)
                return LogInfo.LogErrorMessage(logs, $"XML XPath [{xPath}] did not find a match in [{fileName}]");

            foreach (object node in nodes)
                RenameXmlObject(node, value);

            SaveXml(fileName, doc, XmlFormatMode.Pretty);
            logs.Add(new LogInfo(LogState.Success, $"Renamed XML XPath [{xPath}] in [{fileName}]", cmd));
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

            List<object> nodes = EvaluateXPath(doc, xPath, nsMgr).ToList();
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

            int count = EvaluateXPath(doc, xPath, nsMgr).Count;
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

            string value = string.Join(delim, EvaluateXPath(doc, xPath, nsMgr).Select(XmlObjectToText));
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

        private static List<object> EvaluateXPath(XDocument doc, string xPath, XmlNamespaceManager nsMgr)
        {
            object result = doc.XPathEvaluate(xPath, nsMgr);
            if (result is IEnumerable<object> enumerable)
                return enumerable.ToList();
            return new List<object> { result };
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

        private static void SetXmlObjectValue(object node, string value)
        {
            switch (node)
            {
                case XElement e:
                    e.Value = value;
                    break;
                case XAttribute a:
                    a.Value = value;
                    break;
                case XCData c:
                    c.Value = value;
                    break;
                case XText t:
                    t.Value = value;
                    break;
            }
        }

        private static void AddXmlNode(object node, XmlAddOperation operation, XmlAddType type, string name, string value, XmlNamespaceManager nsMgr)
        {
            if (type == XmlAddType.Attribute)
            {
                if (node is XElement e)
                    e.SetAttributeValue(ResolveXName(name, nsMgr), value);
                return;
            }

            object newNode = type == XmlAddType.Text ? new XText(value) : new XElement(ResolveXName(name, nsMgr), value);
            if (operation == XmlAddOperation.Insert)
            {
                if (node is XElement e)
                    e.AddFirst(newNode);
                else if (node is XNode xNode)
                    xNode.AddBeforeSelf(newNode);
            }
            else if (operation == XmlAddOperation.Append)
            {
                if (node is XElement e)
                    e.Add(newNode);
                else if (node is XNode xNode)
                    xNode.AddAfterSelf(newNode);
            }
            else if (operation == XmlAddOperation.Subnode && node is XElement elem)
            {
                elem.Add(newNode);
            }
        }

        private static XName ResolveXName(string name, XmlNamespaceManager nsMgr)
        {
            int colon = name.IndexOf(':');
            if (colon < 0)
                return name; // no prefix, return as-is

            string prefix = name[..colon];
            string localName = name[(colon + 1)..];

            // Look up both the given prefix and PEBakery's '_' default namespace alias
            string? uri = nsMgr.LookupNamespace(prefix)
                       ?? (prefix == "_" ? nsMgr.LookupNamespace("_") : null);

            return uri != null
                ? XName.Get(localName, uri)  // properly namespaced
                : name;                      // unknown prefix, fall through as-is
        }

        private static void RemoveXmlObject(object node)
        {
            switch (node)
            {
                case XElement e:
                    e.Remove();
                    break;
                case XAttribute a:
                    a.Remove();
                    break;
                case XNode n:
                    n.Remove();
                    break;
            }
        }

        private static void RenameXmlObject(object node, string value)
        {
            switch (node)
            {
                case XElement e:
                    e.Name = XName.Get(value, e.Name.NamespaceName);
                    break;
                case XAttribute a:
                    XElement? parent = a.Parent;
                    if (parent != null)
                    {
                        string attrValue = a.Value;
                        a.Remove();
                        parent.SetAttributeValue(value, attrValue);
                    }
                    break;
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
            SaveOptions options = mode == XmlFormatMode.Compact ? SaveOptions.DisableFormatting : SaveOptions.None;
            File.WriteAllText(fileName, doc.ToString(options), Encoding.UTF8);
        }

        private static List<LogInfo> SetDestVariable(EngineState s, string destVar, string value)
        {
            string escapedValue = StringEscaper.Escape(value, false, true);
            return Variables.SetVariable(s, destVar, escapedValue, false, false, false);
        }
    }
}

