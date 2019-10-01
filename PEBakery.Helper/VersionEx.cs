/*
    Copyright (C) 2016-2019 Hajin Jang
    Licensed under MIT License.
 
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

using System;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;

namespace PEBakery.Helper
{
    #region VersionEx
    /// <summary>
    /// Extended VersionEx to support single integer
    /// Ex) 5 vs 5.1.2600.1234
    /// </summary>
    [Serializable]
    public class VersionEx : IComparable<VersionEx>, IEquatable<VersionEx>, ISerializable
    {
        #region Properties
        public int Major { get; }
        public int Minor { get; }
        public int Build { get; }
        public int Revision { get; }
        #endregion

        #region Construct
        public VersionEx(int major)
        {
            if (major < 0) throw new ArgumentOutOfRangeException(nameof(major));

            Major = major;
            Minor = 0;
            Build = -1;
            Revision = -1;
        }

        public VersionEx(int major, int minor)
        {
            if (major < 0) throw new ArgumentOutOfRangeException(nameof(major));
            if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor));

            Major = major;
            Minor = minor;
            Build = -1;
            Revision = -1;
        }

        public VersionEx(int major, int minor, int build)
        {
            if (major < 0) throw new ArgumentOutOfRangeException(nameof(major));
            if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor));
            if (build < -1) throw new ArgumentOutOfRangeException(nameof(build));

            Major = major;
            Minor = minor;
            Build = build;
            Revision = -1;
        }

        public VersionEx(int major, int minor, int build, int revision)
        {
            if (major < 0) throw new ArgumentOutOfRangeException(nameof(major));
            if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor));
            if (build < -1) throw new ArgumentOutOfRangeException(nameof(build));
            if (revision < -1) throw new ArgumentOutOfRangeException(nameof(revision));

            Major = major;
            Minor = minor;
            Build = build;
            Revision = revision;
        }
        #endregion

        #region Parse
        public static VersionEx Parse(string str)
        {
            if (str == null)
                return null;

            int[] arr = { 0, 0, -1, -1 };

            string[] parts = str.Split('.');
            if (parts.Length < 1 || 4 < parts.Length)
                return null;

            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out arr[i]))
                    return null;
            }

            try { return new VersionEx(arr[0], arr[1], arr[2], arr[3]); }
            catch { return null; }
        }
        #endregion

        #region CompareTo
        public int CompareTo(VersionEx other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));

            if (Major != other.Major)
                return Major > other.Major ? 1 : -1;
            if (Minor != other.Minor)
                return Minor > other.Minor ? 1 : -1;
            if (Build != other.Build)
                return Build > other.Build ? 1 : -1;
            if (Revision != other.Revision)
                return Revision > other.Revision ? 1 : -1;

            return 0;
        }
        #endregion

        #region Equals
        public override bool Equals(object obj)
        {
            if (obj is VersionEx x)
                return CompareTo(x) == 0;
            return false;
        }

        public bool Equals(VersionEx x)
        {
            if (x is null)
                return false;
            return CompareTo(x) == 0;
        }
        #endregion

        #region Operators
        public static bool operator ==(VersionEx v1, VersionEx v2)
        {
            if (v1 is null)
            {
                if (v2 is null)
                    return true;
                else
                    return false;
            }
            else
            {
                if (v2 is null)
                    return false;
                else
                    return v1.CompareTo(v2) == 0;
            }
        }

        public static bool operator !=(VersionEx v1, VersionEx v2)
        {
            return !(v1 == v2);
        }

        public static bool operator <(VersionEx v1, VersionEx v2)
        {
            if (v1 is null)
                throw new ArgumentNullException(nameof(v1));

            return v1.CompareTo(v2) < 0;
        }

        public static bool operator <=(VersionEx v1, VersionEx v2)
        {
            if (v1 is null)
                throw new ArgumentNullException(nameof(v1));

            return v1.CompareTo(v2) <= 0;
        }

        public static bool operator >(VersionEx v1, VersionEx v2)
        {
            return v2 < v1;
        }

        public static bool operator >=(VersionEx v1, VersionEx v2)
        {
            return v2 <= v1;
        }
        #endregion

        #region (Override) ToString, GetHashCode
        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append(Major);
            b.Append('.');
            b.Append(Minor);
            if (Build != -1)
            {
                b.Append('.');
                b.Append(Build);
                if (Revision != -1)
                {
                    b.Append('.');
                    b.Append(Revision);
                }
            }
            return b.ToString();
        }

        public override int GetHashCode()
        {
            return (ushort)Major * 0x1000000 + (ushort)Minor * 0x10000 + (ushort)Build * 0x100 + (ushort)Revision;
        }
        #endregion

        #region Serailizable
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(Major), Major, typeof(int));
            info.AddValue(nameof(Minor), Minor, typeof(int));
            info.AddValue(nameof(Build), Build, typeof(int));
            info.AddValue(nameof(Revision), Revision, typeof(int));
        }

        protected VersionEx(SerializationInfo info, StreamingContext context)
        {
            Major = (int)info.GetValue(nameof(Major), typeof(int));
            Minor = (int)info.GetValue(nameof(Minor), typeof(int));
            Build = (int)info.GetValue(nameof(Build), typeof(int));
            Revision = (int)info.GetValue(nameof(Revision), typeof(int));
        }
        #endregion
    }
    #endregion
}
