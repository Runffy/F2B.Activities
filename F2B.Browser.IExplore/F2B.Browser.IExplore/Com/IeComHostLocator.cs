using System;
using System.Collections.Generic;
using System.IO;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>
    /// Resolves <c>IExplore.ComHost.exe</c> (x86) next to the x64 plugin or under OpenRPA extensions.
    /// Path rules mirror <c>OpenRPA.Interfaces.Extensions.ProjectsDirectory</c> + <c>extensions</c>.
    /// </summary>
    public static class IeComHostLocator
    {
        public const string ComHostFileName = "IExplore.ComHost.exe";
        public const string ComHostSubFolder = "x86";

        /// <summary>Full path to <see cref="ComHostFileName"/>.</summary>
        public static string ResolveComHostPath()
        {
            foreach (var dir in GetSearchDirectories())
            {
                foreach (var candidate in GetComHostCandidates(dir))
                {
                    if (File.Exists(candidate))
                        return Path.GetFullPath(candidate);
                }
            }

            var extDir = ResolveOpenRpaExtensionsDirectory();
            throw new FileNotFoundException(
                "Could not find " + ComHostFileName + ". Deploy IExplore.ComHost.exe under " +
                Path.Combine(extDir, ComHostSubFolder) + " (next to the x64 F2B.Browser.IExplore.dll in " + extDir + ").",
                Path.Combine(ComHostSubFolder, ComHostFileName));
        }

        /// <summary>Working directory for ComHost (folder that contains the x86 F2B.Browser.IExplore.dll).</summary>
        public static string ResolveComHostWorkingDirectory()
        {
            var hostPath = ResolveComHostPath();
            var hostDir = Path.GetDirectoryName(hostPath);

            if (hostDir != null
                && string.Equals(Path.GetFileName(hostDir), ComHostSubFolder, StringComparison.OrdinalIgnoreCase))
            {
                return hostDir;
            }

            var x86Dir = Path.Combine(hostDir ?? string.Empty, ComHostSubFolder);
            if (Directory.Exists(x86Dir))
                return Path.GetFullPath(x86Dir);

            return hostDir;
        }

        /// <summary>
        /// Same rule as OpenRPA: default %Documents%\OpenRPA; use %AppData%\OpenRPA when settings.json exists there.
        /// </summary>
        public static string ResolveOpenRpaProjectsDirectory()
        {
            var myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var myDocsOpenRpa = Path.Combine(myDocs, "OpenRPA");
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appDataOpenRpa = Path.Combine(appData, "OpenRPA");

            if (File.Exists(Path.Combine(appDataOpenRpa, "settings.json")))
                return appDataOpenRpa;

            if (File.Exists(Path.Combine(myDocsOpenRpa, "settings.json")))
                return myDocsOpenRpa;

            return myDocsOpenRpa;
        }

        /// <summary><c>%ProjectsDirectory%\extensions</c> — where OpenRPA loads dependency DLLs.</summary>
        public static string ResolveOpenRpaExtensionsDirectory() =>
            Path.Combine(ResolveOpenRpaProjectsDirectory(), "extensions");

        /// <summary>
        /// Directories to probe: plugin/extensions folder (same dir as x64 DLL), then OpenRPA extensions root.
        /// </summary>
        public static IEnumerable<string> GetSearchDirectories()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();

            void TryAdd(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;
                path = Path.GetFullPath(path);
                if (seen.Add(path))
                    list.Add(path);
            }

            TryAdd(GetPluginAssemblyDirectory());
            TryAdd(ResolveOpenRpaExtensionsDirectory());

            return list;
        }

        private static IEnumerable<string> GetComHostCandidates(string baseDir)
        {
            yield return Path.Combine(baseDir, ComHostSubFolder, ComHostFileName);
            yield return Path.Combine(baseDir, ComHostFileName);
        }

        private static string GetPluginAssemblyDirectory()
        {
            var asm = typeof(IeComHostLocator).Assembly;
            var location = asm.Location;
            if (!string.IsNullOrEmpty(location))
                return Path.GetDirectoryName(location);

            var codeBase = asm.CodeBase;
            if (!string.IsNullOrEmpty(codeBase))
            {
                var uri = new Uri(codeBase);
                return Path.GetDirectoryName(uri.LocalPath);
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
