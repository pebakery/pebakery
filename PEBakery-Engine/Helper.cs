using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PEBakery_Engine
{
    /// <summary>
    /// Contains static helper methods.
    /// </summary>
    public static class Helper
    {
        /// <summary>
        /// Count occurrences of strings.
        /// http://www.dotnetperls.com/string-occurrence
        /// </summary>
        public static int CountStringOccurrences(string text, string pattern)
        {
            // Loop through all instances of the string 'text'.
            int count = 0;
            int i = 0;
            while ((i = text.IndexOf(pattern, i)) != -1)
            {
                i += pattern.Length;
                count++;
            }
            return count;
        }
    }
}
