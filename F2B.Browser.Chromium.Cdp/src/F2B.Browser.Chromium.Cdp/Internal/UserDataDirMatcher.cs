using System;
using System.IO;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class UserDataDirMatcher
    {
        /// <summary>
        /// Determines whether the existing browser on the port uses the same user data dir
        /// as requested (exact path, system profile, or temp/documents pattern).
        /// </summary>
        /// <param name="existingUserDataDir">
        /// User data dir extracted from the running process command line.
        /// Null means the process uses the default system profile.
        /// </param>
        public static bool IsSameUserDataDir(
            string requestedUserDataDir,
            string browserName,
            int port,
            string existingUserDataDir)
        {
            if (UserDataDirResolver.IsSystemProfile(requestedUserDataDir))
            {
                return IsExistingSystemProfile(browserName, existingUserDataDir);
            }

            if (existingUserDataDir == null)
            {
                return false;
            }

            var expectedPath = UserDataDirResolver.ResolveExpectedPath(requestedUserDataDir, browserName, port);
            return string.Equals(
                NormalizePath(existingUserDataDir),
                NormalizePath(expectedPath),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExistingSystemProfile(string browserName, string existingUserDataDir)
        {
            if (existingUserDataDir == null)
            {
                return true;
            }

            var systemPath = UserDataDirResolver.ResolveSystemUserDataDir(browserName);
            return string.Equals(
                NormalizePath(existingUserDataDir),
                NormalizePath(systemPath),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path.Trim('"').Trim())
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim('"').Trim()
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }
    }
}
