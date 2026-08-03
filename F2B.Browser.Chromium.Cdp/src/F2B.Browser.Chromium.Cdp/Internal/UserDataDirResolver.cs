using System;
using System.IO;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class UserDataDirResolver
    {
        public static string Resolve(string userDataDir, string browserName, int port)
        {
            var input = string.IsNullOrWhiteSpace(userDataDir) ? "temp" : userDataDir.Trim();

            if (string.Equals(input, "system", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveSystemUserDataDir(browserName);
            }

            if (string.Equals(input, "temp", StringComparison.OrdinalIgnoreCase))
            {
                return EnsureDirectoryExists(BuildPatternPath(Path.GetTempPath(), browserName, port));
            }

            if (string.Equals(input, "documents", StringComparison.OrdinalIgnoreCase))
            {
                var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return EnsureDirectoryExists(BuildPatternPath(documents, browserName, port));
            }

            return EnsureDirectoryExists(Path.GetFullPath(input));
        }

        public static string ResolveSystemUserDataDir(string browserName)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (string.Equals(browserName, "msedge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(browserName, "edge", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(localAppData, "Microsoft", "Edge", "User Data");
            }

            if (string.Equals(browserName, "chrome", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(localAppData, "Google", "Chrome", "User Data");
            }

            // Fallback for custom browser executables.
            return Path.Combine(localAppData, browserName, "User Data");
        }

        public static bool IsSystemProfile(string userDataDir)
        {
            return string.Equals(userDataDir, "system", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves the expected user data dir path without creating directories.
        /// Used for comparing against an already running browser.
        /// </summary>
        public static string ResolveExpectedPath(string userDataDir, string browserName, int port)
        {
            var input = string.IsNullOrWhiteSpace(userDataDir) ? "temp" : userDataDir.Trim();

            if (string.Equals(input, "system", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveSystemUserDataDir(browserName);
            }

            if (string.Equals(input, "temp", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizePath(BuildPatternPath(Path.GetTempPath(), browserName, port));
            }

            if (string.Equals(input, "documents", StringComparison.OrdinalIgnoreCase))
            {
                var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return NormalizePath(BuildPatternPath(documents, browserName, port));
            }

            return NormalizePath(Path.GetFullPath(input));
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        private static string BuildPatternPath(string rootDir, string browserName, int port)
        {
            return Path.Combine(rootDir, "BrowserData", browserName, port.ToString());
        }

        private static string EnsureDirectoryExists(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                throw new BrowserException(
                    string.Format("Unable to create user data directory: {0}", path), ex);
            }
        }
    }
}
