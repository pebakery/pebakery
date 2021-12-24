/*
    Copyright (C) 2019-2022 Hajin Jang
 
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

using System.Runtime.InteropServices;

namespace PEBakery.Ini
{
    #region (docs) Notes
    /*
     * MSDN states that these APIs are provided only for 16-bit compatibility.
     * https://docs.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-writeprivateprofilestringw
     * 
     * Be careful with paths when using this APis, MSDN also states that:
     *   If the lpFileName parameter does not contain a full path and file name for the file, WritePrivateProfileString searches the Windows directory for the file.
     *   If the file does not exist, this function creates the file in the Windows directory.
     *   If lpFileName contains a full path and file name and the file does not exist, WritePrivateProfileString creates the file.
     *   The specified directory must already exist.
     *   
     * When null is given to lpAppName, 
     *   If no section name matches the string pointed to by the lpAppName parameter,
     *   WritePrivateProfileSection creates the section at the end of the specified initialization file 
     *   and initializes the new section with the specified key name and value pairs.
     */
    #endregion

    public static class NativeMethods
    {
        private const string Kernel32 = "kernel32.dll";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lpAppName"></param>
        /// <param name="lpString"></param>
        /// <param name="lpFileName"></param>
        /// <returns></returns>
        [DllImport(Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal extern static bool WritePrivateProfileSectionW(string lpAppName, string lpString, string lpFileName);

        /// <summary>
        /// Copies a string into the specified section of an initialization file.
        /// </summary>
        /// <param name="lpAppName">The name of the section to which the string will be copied. If the section does not exist, it is created. The name of the section is case-independent; the string can be any combination of uppercase and lowercase letters.</param>
        /// <param name="lpKeyName">The name of the key to be associated with a string. If the key does not exist in the specified section, it is created. If this parameter is NULL, the entire section, including all entries within the section, is deleted.</param>
        /// <param name="lpString">A null-terminated string to be written to the file. If this parameter is NULL, the key pointed to by the lpKeyName parameter is deleted.</param>
        /// <param name="lpFileName">The name of the initialization file.</param>
        /// <returns>
        /// If the function successfully copies the string to the initialization file, the return value is false.
        /// If the function fails, or if it flushes the cached version of the most recently accessed initialization file, the return value is true.
        /// To get extended error information, call GetLastError.
        /// </returns>
        [DllImport(Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal extern static bool WritePrivateProfileStringW(string lpAppName, string lpKeyName, string lpString, string lpFileName);
    }
}
