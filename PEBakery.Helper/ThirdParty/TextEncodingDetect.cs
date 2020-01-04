/*
 * Based on Jonathan Bennett's TextEncodingDetect
 * Copyright 2015-2016 Jonathan Bennett<jon@autoitscript.com>
 *
 * https://github.com/AutoItConsulting/text-encoding-detect
 * https://www.autoitconsulting.com/site/development/utf-8-utf-16-text-encoding-detection-library/
 *
 * Maintained by Hajin Jang
 * 
 * https://www.autoitscript.com 
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;

namespace PEBakery.Helper.ThirdParty
{
    #region TextEncoding
    public enum TextEncoding
    {
        None, // Unknown or binary
        Ansi, // 0-255
        Ascii, // 0-127
        Utf8Bom, // UTF8 with BOM
        Utf8NoBom, // UTF8 without BOM
        Utf16LeBom, // UTF16 LE with BOM
        Utf16LeNoBom, // UTF16 LE without BOM
        Utf16BeBom, // UTF16-BE with BOM
        Utf16BeNoBom // UTF16-BE without BOM
    }
    #endregion

    #region TextEncodingDetect
    public class TextEncodingDetect
    {
        #region BOM Sequences
        private readonly byte[] _utf16BeBom =
        {
            0xFE,
            0xFF
        };

        private readonly byte[] _utf16LeBom =
        {
            0xFF,
            0xFE
        };

        private readonly byte[] _utf8Bom =
        {
            0xEF,
            0xBB,
            0xBF
        };
        #endregion

        #region Properties
        private bool _nullSuggestsBinary = true;
        private double _utf16ExpectedNullPercent = 70;
        private double _utf16UnexpectedNullPercent = 10;

        /// <summary>
        /// Sets if the presence of nulls in a buffer indicate the buffer is binary data rather than text.
        /// </summary>
        public bool NullSuggestsBinary
        {
            set => _nullSuggestsBinary = value;
        }

        public double Utf16ExpectedNullPercent
        {
            set
            {
                if (value > 0 && value < 100)
                    _utf16ExpectedNullPercent = value;
            }
        }

        public double Utf16UnexpectedNullPercent
        {
            set
            {
                if (value > 0 && value < 100)
                    _utf16UnexpectedNullPercent = value;
            }
        }
        #endregion

        #region GetBomLengthFromEncodingMode
        /// <summary>
        ///     Gets the BOM length for a given Encoding mode.
        /// </summary>
        /// <param name="encoding"></param>
        /// <returns>The BOM length.</returns>
        public static int GetBomLengthFromEncodingMode(TextEncoding encoding)
        {
            int length;

            switch (encoding)
            {
                case TextEncoding.Utf16BeBom:
                case TextEncoding.Utf16LeBom:
                    length = 2;
                    break;
                case TextEncoding.Utf8Bom:
                    length = 3;
                    break;
                default:
                    length = 0;
                    break;
            }

            return length;
        }
        #endregion

        #region Encoding Detection
        /// <summary>
        /// Checks for a BOM sequence in a byte buffer.
        /// </summary>
        /// <returns>Encoding type or Encoding.None if no BOM.</returns>
        public TextEncoding CheckBom(byte[] buffer, int offset, int count)
        {
            return CheckBom(buffer.AsSpan(offset, count));
        }

        /// <summary>
        /// Checks for a BOM sequence in a byte buffer.
        /// </summary>
        /// <returns>Encoding type or Encoding.None if no BOM.</returns>
        public TextEncoding CheckBom(ReadOnlySpan<byte> span)
        {
            // Check for BOM
            if (2 <= span.Length && span[0] == _utf16LeBom[0] && span[1] == _utf16LeBom[1])
                return TextEncoding.Utf16LeBom;

            if (2 <= span.Length && span[0] == _utf16BeBom[0] && span[1] == _utf16BeBom[1])
                return TextEncoding.Utf16BeBom;

            if (3 <= span.Length && span[0] == _utf8Bom[0] && span[1] == _utf8Bom[1] && span[2] == _utf8Bom[2])
                return TextEncoding.Utf8Bom;

            return TextEncoding.None;
        }

        public TextEncoding DetectEncoding(byte[] buffer, int offset, int count)
        {
            return DetectEncoding(buffer.AsSpan(offset, count));
        }

        /// <summary>
        ///     Automatically detects the Encoding type of a given byte buffer.
        /// </summary>
        /// <param name="span">The byte buffer.</param>
        /// <param name="size">The size of the byte buffer.</param>
        /// <returns>The Encoding type or Encoding.None if unknown.</returns>
        public TextEncoding DetectEncoding(ReadOnlySpan<byte> span)
        {
            // First check if we have a BOM and return that if so
            TextEncoding encoding = CheckBom(span);
            if (encoding != TextEncoding.None)
            {
                return encoding;
            }

            // Now check for valid UTF8
            encoding = CheckUtf8(span);
            if (encoding != TextEncoding.None)
            {
                return encoding;
            }

            // Now try UTF16 
            encoding = CheckUtf16NewlineChars(span);
            if (encoding != TextEncoding.None)
            {
                return encoding;
            }

            encoding = CheckUtf16Ascii(span);
            if (encoding != TextEncoding.None)
            {
                return encoding;
            }

            // ANSI or None (binary) then
            if (!DoesContainNulls(span))
            {
                return TextEncoding.Ansi;
            }

            // Found a null, return based on the preference in null_suggests_binary_
            return _nullSuggestsBinary ? TextEncoding.None : TextEncoding.Ansi;
        }

        /// <summary>
        /// Checks if a buffer contains text that looks like utf16 by scanning for
        /// newline chars that would be present even in non-english text.
        /// </summary>
        /// <param name="span">The byte buffer.</param>
        /// <returns>Encoding.none, Encoding.Utf16LeNoBom or Encoding.Utf16BeNoBom.</returns>
        private static TextEncoding CheckUtf16NewlineChars(ReadOnlySpan<byte> span)
        {
            if (span.Length < 2)
                return TextEncoding.None;

            // Reduce size by 1 so we don't need to worry about bounds checking for pairs of bytes
            int size = span.Length - 1;

            var leControlChars = 0;
            var beControlChars = 0;

            int pos = 0;
            while (pos < size)
            {
                byte ch1 = span[pos++];
                byte ch2 = span[pos++];

                if (ch1 == 0)
                {
                    if (ch2 == 0x0a || ch2 == 0x0d)
                    {
                        ++beControlChars;
                    }
                }
                else if (ch2 == 0)
                {
                    if (ch1 == 0x0a || ch1 == 0x0d)
                    {
                        ++leControlChars;
                    }
                }

                // If we are getting both LE and BE control chars then this file is not utf16
                if (leControlChars > 0 && beControlChars > 0)
                    return TextEncoding.None;
            }

            if (leControlChars > 0)
                return TextEncoding.Utf16LeNoBom;

            return beControlChars > 0 ? TextEncoding.Utf16BeNoBom : TextEncoding.None;
        }

        /// <summary>
        /// Checks if a buffer contains any nulls. Used to check for binary vs text data.
        /// </summary>
        /// <param name="span">The byte buffer.</param>
        private static bool DoesContainNulls(ReadOnlySpan<byte> span)
        {
            int pos = 0;
            while (pos < span.Length)
            {
                if (span[pos++] == 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a buffer contains text that looks like utf16. This is done based
        /// on the use of nulls which in ASCII/script like text can be useful to identify.
        /// </summary>
        /// <param name="buffer">The byte buffer.</param>
        /// <returns>Encoding.none, Encoding.Utf16LeNoBom or Encoding.Utf16BeNoBom.</returns>
        private TextEncoding CheckUtf16Ascii(ReadOnlySpan<byte> buffer)
        {
            int numOddNulls = 0;
            int numEvenNulls = 0;

            // Get even nulls
            int pos = 0;
            while (pos < buffer.Length)
            {
                if (buffer[pos] == 0)
                    numEvenNulls += 1;

                pos += 2;
            }

            // Get odd nulls
            pos = 1;
            while (pos < buffer.Length)
            {
                if (buffer[pos] == 0)
                    numOddNulls++;

                pos += 2;
            }

            double evenNullThreshold = numEvenNulls * 2.0 / buffer.Length;
            double oddNullThreshold = numOddNulls * 2.0 / buffer.Length;
            double expectedNullThreshold = _utf16ExpectedNullPercent / 100.0;
            double unexpectedNullThreshold = _utf16UnexpectedNullPercent / 100.0;

            // Lots of odd nulls, low number of even nulls
            if (evenNullThreshold < unexpectedNullThreshold && oddNullThreshold > expectedNullThreshold)
                return TextEncoding.Utf16LeNoBom;

            // Lots of even nulls, low number of odd nulls
            if (oddNullThreshold < unexpectedNullThreshold && evenNullThreshold > expectedNullThreshold)
                return TextEncoding.Utf16BeNoBom;

            // Don't know
            return TextEncoding.None;
        }

        /// <summary>
        /// Checks if a buffer contains valid utf8.
        /// </summary>
        /// <param name="buffer">The byte buffer.</param>
        /// <returns>
        /// Encoding type of Encoding.None (invalid UTF8), Encoding.Utf8NoBom (valid utf8 multibyte strings) or
        /// Encoding.ASCII (data in 0.127 range).
        /// </returns>
        /// <returns>2</returns>
        private TextEncoding CheckUtf8(ReadOnlySpan<byte> buffer)
        {
            // UTF8 Valid sequences
            // 0xxxxxxx  ASCII
            // 110xxxxx 10xxxxxx  2-byte
            // 1110xxxx 10xxxxxx 10xxxxxx  3-byte
            // 11110xxx 10xxxxxx 10xxxxxx 10xxxxxx  4-byte
            //
            // Width in UTF8
            // Decimal      Width
            // 0-127        1 byte
            // 194-223      2 bytes
            // 224-239      3 bytes
            // 240-244      4 bytes
            //
            // Subsequent chars are in the range 128-191
            bool onlySawAsciiRange = true;
            int pos = 0;

            while (pos < buffer.Length)
            {
                byte ch = buffer[pos++];

                if (ch == 0 && _nullSuggestsBinary)
                {
                    return TextEncoding.None;
                }

                int moreChars;
                if (ch <= 127) // 1 byte
                    moreChars = 0;
                else if (ch >= 194 && ch <= 223) // 2 Byte
                    moreChars = 1;
                else if (ch >= 224 && ch <= 239) // 3 Byte
                    moreChars = 2;
                else if (ch >= 240 && ch <= 244) // 4 Byte
                    moreChars = 3;
                else // Not utf8
                    return TextEncoding.None;

                // Check secondary chars are in range if we are expecting any
                while (moreChars > 0 && pos < buffer.Length)
                {
                    onlySawAsciiRange = false; // Seen non-ascii chars now

                    ch = buffer[pos++];
                    if (ch < 128 || ch > 191)
                        return TextEncoding.None; // Not utf8

                    moreChars -= 1;
                }
            }

            // If we get to here then only valid UTF-8 sequences have been processed
            // If we only saw chars in the range 0-127 then we can't assume UTF8 (the caller will need to decide)
            return onlySawAsciiRange ? TextEncoding.Ascii : TextEncoding.Utf8NoBom;
        }
        #endregion
    }
    #endregion
}