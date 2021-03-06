﻿using System.Linq;

namespace ECMA2Yaml
{
    public static class StringExtensions
    {
        /// <summary>Defaults to <see cref="F:System.IO.Path.DirectorySeparatorChar"/>.
        /// Should only be changed for unit testing purposes.</summary>
        public static char DirectorySeparatorChar = System.IO.Path.DirectorySeparatorChar;

        public static string NormalizePath(this string path)
        {
            if (path == null) return path;

            char otherSepChar = '/';

            if (DirectorySeparatorChar == '/')
                otherSepChar = '\\';

            if (path.Contains(otherSepChar))
                path = path.Replace(otherSepChar, DirectorySeparatorChar);

            return path;
        }
    }
}
