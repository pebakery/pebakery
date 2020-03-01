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

using Newtonsoft.Json;
using System;

namespace PEBakery.Helper
{
    #region VersionExJsonConverter
    /// <summary>
    /// Newtonsoft.Json JsonConverter for VersionEx
    /// </summary>
    public class VersionExJsonConverter : JsonConverter<VersionEx>
    {
        public override void WriteJson(JsonWriter writer, VersionEx value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override VersionEx ReadJson(JsonReader reader, Type objectType, VersionEx existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.Value is string str)
                return VersionEx.Parse(str);
            else
                return null;
        }
    }
    #endregion

    #region VersionJsonConverter
    /// <summary>
    /// Newtonsoft.Json JsonConverter for VersionEx
    /// </summary>
    public class VersionJsonConverter : JsonConverter<Version>
    {
        public override void WriteJson(JsonWriter writer, Version value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override Version ReadJson(JsonReader reader, Type objectType, Version existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.Value is string str)
            {
                try { return Version.Parse(str); }
                catch { return null; }
            }
            else
            {
                return null;
            }
        }
    }
    #endregion
}
