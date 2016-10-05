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
        public List<LogInfo> TXTAddLine(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 3, optional operand : 1
            const int necessaryOperandNum = 3;
            const int optionalOperandNum = 1;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            // Get operands
            string fileName = UnescapeString(ExpandVariables(cmd.Operands[0]));
            string rawFileName = cmd.Operands[0];
            string line = UnescapeString(ExpandVariables(cmd.Operands[1]));
            int mode = 1;
            int placeLineNum = 0;

            if (string.Equals(cmd.Operands[2], "Prepend", StringComparison.OrdinalIgnoreCase))
            {
                mode = 0;
                if (necessaryOperandNum < cmd.Operands.Count)
                    throw new InvalidOperandException("Too many operands");
            }
            else if (string.Equals(cmd.Operands[2], "Append", StringComparison.OrdinalIgnoreCase))
            {
                mode = 1;
                if (necessaryOperandNum < cmd.Operands.Count)
                    throw new InvalidOperandException("Too many operands");
            }
            else if (string.Equals(cmd.Operands[2], "Place", StringComparison.OrdinalIgnoreCase))
            {
                mode = 2;
                if (necessaryOperandNum + 1 < cmd.Operands.Count)
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
                Helper.WriteTextBOM(new FileStream(fileName, FileMode.Create, FileAccess.Write), Encoding.UTF8).Close();
                

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
                logs.Add(new LogInfo(cmd, LogState.Success, $"Prepened [{line}] to [{rawFileName}]"));
            }
            else if (mode == 1) // Append
            {
                File.AppendAllText(fileName, line + "\r\n", encoding);
                logs.Add(new LogInfo(cmd, LogState.Success, $"Appended [{line}] to [{rawFileName}]"));
            }
            else if (mode == 2) // Place
            { // In Place mode, placeLineNum starts from 1;
                int count = 1;
                string temp = Helper.CreateTempFile();
                StreamReader sr = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read), encoding);
                StreamWriter sw = new StreamWriter(new FileStream(temp, FileMode.Create, FileAccess.Write), encoding);
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
                logs.Add(new LogInfo(cmd, LogState.Success, $"Placed [{line}] to [{placeLineNum}]th row of [{rawFileName}]"));
            }

            return logs;
        }

        /// <summary>
        /// IniRead,<FileName>,<Section>,<Key>,<%Variable%> 
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public List<LogInfo> INIRead(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 4, optional operand : 0
            const int necessaryOperandNum = 4;
            const int optionalOperandNum = 0;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            // Get operands
            string fileName = UnescapeString(ExpandVariables(cmd.Operands[0]));
            string section = cmd.Operands[1]; // 문서화 : 여기 값은 변수 Expand 안한다.
            string key = UnescapeString(cmd.Operands[2]); // 문서화 : 여기 값은 변수 Expand는 안 하나, but do escaping.
            string varName = cmd.Operands[3].Trim('%');
            string rawFileName = cmd.Operands[0];

            if (string.Equals(section, string.Empty, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperandException("Section name can not be empty", cmd);
            if (string.Equals(key, string.Empty, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperandException("Key name can not be empty", cmd);

            try
            {
                string value = IniFile.GetKey(fileName, section, key);
                if (value != null)
                variables.SetValue(VarsType.Local, varName, value, cmd.Depth);
                logs.Add(new LogInfo(cmd, LogState.Success, $"Var [%{varName}%] set to [{value}], read from [{rawFileName}]"));
            }
            catch (FileNotFoundException)
            {
                logs.Add(new LogInfo(cmd, LogState.Error, $"File [{rawFileName}] does not exists"));
            }

            return logs;
        }

        /// <summary>
        /// IniWrite,<FileName>,<Section>,<Key>,<Value> 
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public List<LogInfo> INIWrite(BakeryCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            // Necessary operand : 4, optional operand : 0
            const int necessaryOperandNum = 4;
            const int optionalOperandNum = 0;
            if (cmd.Operands.Count < necessaryOperandNum)
                throw new InvalidOperandException("Necessary operands does not exist", cmd);
            else if (necessaryOperandNum + optionalOperandNum < cmd.Operands.Count)
                throw new InvalidOperandException("Too many operands", cmd);

            // Get operands
            string fileName = UnescapeString(ExpandVariables(cmd.Operands[0]));
            string section = cmd.Operands[1]; // 문서화 : 여기 값은 변수 Expand 안한다.
            string key = UnescapeString(cmd.Operands[2]); // 문서화 : 여기 값은 변수 Expand 안한다, but do escaping.
            string value = UnescapeString(ExpandVariables(cmd.Operands[3]));
            string rawFileName = cmd.Operands[0];

            if (string.Equals(section, string.Empty, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperandException("Section name can not be empty", cmd);
            if (string.Equals(key, string.Empty, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperandException("Key name can not be empty", cmd);

            bool result = IniFile.SetKey(fileName, section, key, value);
            if (result)
                logs.Add(new LogInfo(cmd, LogState.Success, $"Key [{key}] and its value [{value}] wrote to [{rawFileName}]"));
            else
                logs.Add(new LogInfo(cmd, LogState.Error, $"Could not wrote key [{key}] and its value [{value}] to [{rawFileName}]"));
            return logs;
        }
    }
}