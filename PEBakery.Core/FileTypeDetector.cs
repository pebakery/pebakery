/*
    Copyright (C) 2019 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using System;
using Joveler.FileMagician;

namespace PEBakery.Core
{
    /// <summary>
    /// Wrapper class of Joveler.FileMagician library.
    /// </summary>
    public static class FileTypeDetector
    {
        #region IsText
        /// <summary>
        /// Check if a file is a text or binary using libmagic.
        /// </summary>
        public static bool IsText(string filePath)
        {
            string ret;
            using (Magic magic = Magic.Open(Global.MagicFile, MagicFlags.MIME_ENCODING))
            {
                ret = magic.CheckFile(filePath);
            }

            return !ret.Equals("binary", StringComparison.Ordinal);
        }

        /// <summary>
        /// Check if a file is a text or binary using libmagic.
        /// </summary>
        public static bool IsText(Span<byte> span)
        {
            string ret;
            using (Magic magic = Magic.Open(Global.MagicFile, MagicFlags.MIME_ENCODING))
            {
                ret = magic.CheckBuffer(span);
            }

            return !ret.Equals("binary", StringComparison.Ordinal);
        }
        #endregion
    }
}
