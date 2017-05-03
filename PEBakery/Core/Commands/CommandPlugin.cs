using PEBakery.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public static class CommandPlugin
    {
        public static List<LogInfo> ExtractFile(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Trace.Assert(cmd.Info.GetType() == typeof(CodeInfo_ExtractFile));
            CodeInfo_ExtractFile info = cmd.Info as CodeInfo_ExtractFile;

            string pluginFile = StringEscaper.Unescape(info.PluginFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string extractTo = StringEscaper.Preprocess(s, info.ExtractTo);

            bool inCurrentPlugin = false;
            if (info.PluginFile.Equals("%PluginFile%", StringComparison.OrdinalIgnoreCase))
                inCurrentPlugin = true;
            else if (info.PluginFile.Equals("%ScriptFile%", StringComparison.OrdinalIgnoreCase))
                inCurrentPlugin = true;

            Plugin targetPlugin;
            if (inCurrentPlugin)
            {
                targetPlugin = s.CurrentPlugin;
            }
            else
            {
                string fullPath = StringEscaper.ExpandVariables(s, pluginFile);
                targetPlugin = s.Project.GetPluginByFullPath(fullPath);
                if (targetPlugin == null)
                    throw new ExecuteException($"No plugin in [{fullPath}]");
            }

            using (MemoryStream ms = EncodedFile.ExtractFile(targetPlugin, info.DirName, info.FileName))
            using (FileStream fs = new FileStream(Path.Combine(extractTo, fileName), FileMode.Create, FileAccess.Write))
            {
                ms.Position = 0;
                ms.CopyTo(fs);
                ms.Close();
                fs.Close();
            }

            logs.Add(new LogInfo(LogState.Success, $"Encoded file [{fileName}] extracted to [{extractTo}]"));

            return logs;
        }
    }
}
