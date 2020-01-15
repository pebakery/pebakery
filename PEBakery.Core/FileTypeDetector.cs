/*
    Copyright (C) 2019-2020 Hajin Jang
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

using Joveler.FileMagician;
using System;

namespace PEBakery.Core
{
    /// <inheritdoc />
    /// <summary>
    /// Wrapper class of Joveler.FileMagician library.
    /// Magic database is lazy-loaded.
    /// </summary>
    public class FileTypeDetector : IDisposable
    {
        #region Fields and Properties
        private Magic _magic;
        private bool _magicFileLoaded;
        private readonly string _magicFile;
        private readonly object _lock = new object();
        #endregion

        #region Constructor
        public FileTypeDetector(string magicFile)
        {
            _magicFile = magicFile;
            _magicFileLoaded = false;

            _magic = Magic.Open();
        }
        #endregion

        #region Disposable Pattern
        ~FileTypeDetector()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            lock (_lock)
            {
                _magic?.Dispose();
                _magic = null;
            }
        }
        #endregion

        #region FileType
        /// <summary>
        /// Get file type of file.
        /// </summary>
        public string FileType(string filePath)
        {
            return CheckFile(filePath, MagicFlags.NONE);
        }

        /// <summary>
        /// Get file type of buffer.
        /// </summary>
        public string FileType(ReadOnlySpan<byte> span)
        {
            return CheckBuffer(span, MagicFlags.NONE);
        }
        #endregion

        #region MimeType
        /// <summary>
        /// Get mime type of file.
        /// </summary>
        public string MimeType(string filePath)
        {
            return CheckFile(filePath, MagicFlags.MIME_TYPE);
        }

        /// <summary>
        /// Get mime type of buffer.
        /// </summary>
        public string MimeType(ReadOnlySpan<byte> span)
        {
            return CheckBuffer(span, MagicFlags.MIME_TYPE);
        }
        #endregion

        #region IsText
        /// <summary>
        /// Check if a file is a text or binary.
        /// </summary>
        public bool IsText(string filePath)
        {
            string ret = CheckFile(filePath, MagicFlags.MIME_ENCODING);
            return !ret.Equals("binary", StringComparison.Ordinal);
        }

        /// <summary>
        /// Check if a file is a text or binary.
        /// </summary>
        public bool IsText(ReadOnlySpan<byte> span)
        {
            string ret = CheckBuffer(span, MagicFlags.MIME_ENCODING);
            return !ret.Equals("binary", StringComparison.Ordinal);
        }
        #endregion

        #region Utility
        /// <summary>
        /// Get file type of file.
        /// </summary>
        private string CheckFile(string filePath, MagicFlags flags)
        {
            lock (_lock)
            {
                if (!_magicFileLoaded)
                {
                    _magic.Load(_magicFile);
                    _magicFileLoaded = true;
                }

                _magic.SetFlags(flags);
                return _magic.CheckFile(filePath);
            }
        }

        /// <summary>
        /// Get file type of buffer.
        /// </summary>
        private string CheckBuffer(ReadOnlySpan<byte> span, MagicFlags flags)
        {
            lock (_lock)
            {
                if (!_magicFileLoaded)
                {
                    _magic.Load(_magicFile);
                    _magicFileLoaded = true;
                }

                _magic.SetFlags(flags);
                return _magic.CheckBuffer(span);
            }
        }
        #endregion
    }
}
