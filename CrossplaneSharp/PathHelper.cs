using System;
using System.IO;

namespace CrossplaneSharp
{
    /// <summary>
    /// Cross-platform path utilities for NGINX config file resolution.
    /// NGINX config files always use forward-slash separators regardless of OS.
    /// This helper normalises between NGINX-style paths and OS-native paths so
    /// that parsing works correctly on both Unix and Windows.
    /// </summary>
    internal static class PathHelper
    {
        /// <summary>
        /// <c>true</c> on Windows where the file system is case-insensitive and
        /// the path separator is <c>\</c>.
        /// </summary>
        private static readonly bool IsWindows =
            Path.DirectorySeparatorChar == '\\';

        /// <summary>
        /// StringComparer suitable for comparing file-system paths on the current OS.
        /// Case-insensitive on Windows, ordinal (case-sensitive) on Unix.
        /// </summary>
        public static readonly StringComparer PathComparer =
            IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        /// <summary>
        /// Converts any path to use the OS-native directory separator so that
        /// <see cref="Path"/> APIs work correctly on both Unix and Windows.
        /// Forward slashes from NGINX config <c>include</c> directives are
        /// converted to backslashes on Windows and vice-versa.
        /// </summary>
        public static string ToNative(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return IsWindows
                ? path.Replace('/', '\\')
                : path.Replace('\\', '/');
        }

        /// <summary>
        /// Converts a path to use forward slashes (NGINX / Unix style).
        /// Useful when storing paths in <see cref="ConfigFile.File"/> so they
        /// are consistent across platforms.
        /// </summary>
        public static string ToForwardSlash(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return path.Replace('\\', '/');
        }

        /// <summary>
        /// Returns the absolute, OS-native form of <paramref name="path"/>,
        /// handling forward-slash paths on Windows and backslash paths on Unix.
        /// </summary>
        public static string GetFullPath(string path)
        {
            return Path.GetFullPath(ToNative(path));
        }

        /// <summary>
        /// Combines <paramref name="basePath"/> with <paramref name="relativePath"/>,
        /// normalising separators for the current OS first.
        /// </summary>
        public static string Combine(string basePath, string relativePath)
        {
            return Path.Combine(ToNative(basePath), ToNative(relativePath));
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="path"/> is rooted on the current OS,
        /// also recognising Unix-style absolute paths (starting with <c>/</c>) on Windows.
        /// </summary>
        public static bool IsPathRooted(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            // A path starting with '/' is always absolute (Unix absolute or UNC-like)
            if (path[0] == '/') return true;
            return Path.IsPathRooted(path);
        }
    }
}

