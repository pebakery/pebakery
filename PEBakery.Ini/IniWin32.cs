/*
    Copyright (C) 2019 Hajin Jang
 
    MIT License

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using System.IO;

namespace PEBakery.Ini
{
    /// <summary>
    /// Win32 API version of IniReadWriter. Provided for compatibility.
    /// </summary>
    public static class IniWin32
    {
        #region WriteKey
        /// <summary>
        /// Write a pair of key and value into .ini file
        /// </summary>
        /// <returns>If the operation was successful, the function returns true.</returns>
        public static bool WriteKey(string filePath, string section, string key, string value)
        {
            // Make sure to use full path to avoid disrupting Windows directory.
            string fullPath = Path.GetFullPath(filePath);
            return NativeMethods.WritePrivateProfileStringW(section, key, value, fullPath);
        }

        /// <summary>
        /// Write a pair of key and value into an ini file
        /// </summary>
        /// <returns>If the operation was successful, the function returns true.</returns>
        public static bool WriteKey(string filePath, IniKey iniKey)
        {
            // Make sure to use full path to avoid disrupting Windows directory.
            string fullPath = Path.GetFullPath(filePath);
            return NativeMethods.WritePrivateProfileStringW(iniKey.Section, iniKey.Key, iniKey.Value, fullPath);
        }
        #endregion

        #region DeleteKey
        /// <summary>
        /// Delete a pair of key and value from an .ini file
        /// </summary>
        /// <returns>If the operation was successful, the function returns true.</returns>
        public static bool DeleteKey(string filePath, string section, string key)
        {
            // Make sure to use full path to avoid disrupting Windows directory.
            string fullPath = Path.GetFullPath(filePath);
            return NativeMethods.WritePrivateProfileStringW(section, key, null, fullPath);
        }

        /// <summary>
        /// Delete a pair of key and value from an .ini file
        /// </summary>
        /// <returns>If the operation was successful, the function returns true.</returns>
        public static bool DeleteKey(string filePath, IniKey iniKey)
        {
            // Make sure to use full path to avoid disrupting Windows directory.
            string fullPath = Path.GetFullPath(filePath);
            return NativeMethods.WritePrivateProfileStringW(iniKey.Section, iniKey.Key, null, fullPath);
        }
        #endregion

        #region DeleteSection
        /// <summary>
        /// Delete a section from an .ini file.
        /// </summary>
        /// <returns>If the operation was successful, the function returns true.</returns>
        public static bool DeleteSection(string filePath, string section)
        {
            // Make sure to use full path to avoid disrupting Windows directory.
            string fullPath = Path.GetFullPath(filePath);
            return NativeMethods.WritePrivateProfileStringW(section, null, null, fullPath);
        }

        /// <summary>
        /// Delete a section from an .ini file.
        /// </summary>
        /// <returns>If the operation was successful, the function returns true.</returns>
        public static bool DeleteSection(string filePath, IniKey iniKey)
        {
            // Make sure to use full path to avoid disrupting Windows directory.
            string fullPath = Path.GetFullPath(filePath);
            return NativeMethods.WritePrivateProfileStringW(iniKey.Section, null, null, fullPath);
        }
        #endregion
    }
}
