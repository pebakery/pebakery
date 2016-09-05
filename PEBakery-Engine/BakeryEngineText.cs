using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;

namespace BakeryEngine
{
    /// <summary>
    /// Text, INI commands
    /// </summary>
    public partial class BakeryEngine
    {
        /* 
         * Text Manipulation
         */

        /// <summary>
        /// TXTAddLine,<FileName>,<Line>,<Mode>
        /// Mode : Prepend / Append / Place,LineNum
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo[] TXTAddLine(BakeryCommand cmd)
        {
            ArrayList logs = new ArrayList();

            // Necessary operand : 3, optional operand : 1
            const int necessaryOperandNum = 3;
            const int optionalOperandNum = 1;
            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Length)
                throw new InvalidOperandException("Too many operands", cmd);

            // Get operands
            string fileName = variables.Expand(cmd.Operands[0]);
            string rawFileName = cmd.Operands[0];
            string line = variables.Expand(cmd.Operands[1]);
            int mode = 1;
            int placeLineNum = 0;

            if (string.Equals(cmd.Operands[2], "Prepend", StringComparison.OrdinalIgnoreCase))
            {
                mode = 0;
                if (necessaryOperandNum < cmd.Operands.Length)
                    throw new InvalidOperandException("Too many operands");
            }
            else if (string.Equals(cmd.Operands[2], "Append", StringComparison.OrdinalIgnoreCase))
            {
                mode = 1;
                if (necessaryOperandNum < cmd.Operands.Length)
                    throw new InvalidOperandException("Too many operands");
            }
            else if (string.Equals(cmd.Operands[2], "Place", StringComparison.OrdinalIgnoreCase))
            {
                mode = 2;
                if (necessaryOperandNum + 1 < cmd.Operands.Length)
                    throw new InvalidOperandException("Too many operands");
                placeLineNum = int.Parse(cmd.Operands[3]);
                if (placeLineNum <= 0) // In Place mode, placeLineNum starts from 1;
                    throw new InvalidOperandException("Invalid LineNum value. LineNum starts from 1.");
            }
            else
            {
                throw new InvalidOperandException("Invalid mode of TXTADDLine");
            }

            // Detect encoding of text
            // If text does not exists, create blank file
            Encoding encoding = Encoding.UTF8;
            if (File.Exists(fileName))
                encoding = Helper.DetectTextEncoding(fileName);
            else
                Helper.WriteTextBOM(new FileStream(fileName, FileMode.Create, FileAccess.Write), Encoding.UTF8);

            if (mode == 0) // Prepend
            {
                string temp = Helper.CreateTempFile();
                StreamReader sr = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read), encoding);
                StreamWriter sw = new StreamWriter(new FileStream(fileName, FileMode.Create, FileAccess.Write), encoding);
                sw.WriteLine(line);
                string lineFromSrc;
                while ((lineFromSrc = sr.ReadLine()) != null)
                    sw.WriteLine(lineFromSrc);
                sr.Close();
                sw.Close();
                Helper.FileReplaceEx(temp, fileName);
                logs.Add(new LogInfo(cmd, string.Concat("Prepened [", line, "] to [", rawFileName, "]"), LogState.Success));
            }
            else if (mode == 1) // Append
            {
                File.AppendAllText(fileName, line + "\r\n", encoding);
                logs.Add(new LogInfo(cmd, string.Concat("Appended [", line, "] to [", rawFileName, "]"), LogState.Success));
            }
            else if (mode == 2) // Place
            { // In Place mode, placeLineNum starts from 1;
                int count = 1;
                string temp = Helper.CreateTempFile();
                StreamReader sr = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read), encoding);
                StreamWriter sw = new StreamWriter(new FileStream(fileName, FileMode.Create, FileAccess.Write), encoding);
                string lineFromSrc;
                while ((lineFromSrc = sr.ReadLine()) != null)
                {
                    if (count == placeLineNum)
                        sw.WriteLine(line);
                    sw.WriteLine(lineFromSrc);
                    count++;
                }
                sr.Close();
                sw.Close();
                Helper.FileReplaceEx(temp, fileName);
                logs.Add(new LogInfo(cmd, string.Concat("Placed [", line, "] to [", placeLineNum, "]th row of [", rawFileName, "]"), LogState.Success));
            }

            return logs.ToArray(typeof(LogInfo)) as LogInfo[];
        }

        /// <summary>
        /// IniRead,<FileName>,<Section>,<Key>,<%Variable%> 
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo[] INIRead(BakeryCommand cmd)
        {
            ArrayList logs = new ArrayList();

            // Necessary operand : 4, optional operand : 0
            const int necessaryOperandNum = 4;
            const int optionalOperandNum = 0;
            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Length)
                throw new InvalidOperandException("Too many operands", cmd);

            // Get operands
            string fileName = variables.Expand(cmd.Operands[0]);
            string section = variables.Expand(cmd.Operands[1]);
            string key = variables.Expand(cmd.Operands[2]);
            string varName = cmd.Operands[3].Trim('%');
            string rawFileName = cmd.Operands[0];

            try
            {
                string value = IniFile.GetIniKey(fileName, section, key);
                if (value != null)
                variables.SetValue(VarsType.Local, varName, value);
                logs.Add(new LogInfo(cmd, string.Concat("[%", varName, "%] set to [", value, "], read from [", rawFileName, "]"), LogState.Success));
            }
            catch (FileNotFoundException)
            {
                logs.Add(new LogInfo(cmd, string.Concat("[", rawFileName, "] does not exists"), LogState.Error));
            }

            return logs.ToArray(typeof(LogInfo)) as LogInfo[];
        }

        /// <summary>
        /// IniWrite,<FileName>,<Section>,<Key>,<Value> 
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public LogInfo[] INIWrite(BakeryCommand cmd)
        {
            ArrayList logs = new ArrayList();

            // Necessary operand : 4, optional operand : 0
            const int necessaryOperandNum = 4;
            const int optionalOperandNum = 0;
            if (cmd.Operands.Length < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Length)
                throw new InvalidOperandException("Too many operands", cmd);

            // Get operands
            string fileName = variables.Expand(cmd.Operands[0]);
            string section = variables.Expand(cmd.Operands[1]);
            string key = variables.Expand(cmd.Operands[2]);
            string value = variables.Expand(cmd.Operands[3]);
            string rawFileName = cmd.Operands[0];

            bool result = IniFile.SetIniKey(fileName, section, key, value);
            if (result)
                logs.Add(new LogInfo(cmd, string.Concat("Key [", key, "] and its value [", value, "]' wrote to [", rawFileName, "]"), LogState.Success));
            else
                logs.Add(new LogInfo(cmd, string.Concat("Could not wrote key [", key, "] and its value [", value, "] to [", rawFileName, "]"), LogState.Error));
            return logs.ToArray(typeof(LogInfo)) as LogInfo[];
        }
    }
}