using System;
using System.Diagnostics;
using System.IO;

namespace F2B.Terminal.PCOMM
{
    public static class PcommWsLauncher
    {
        public const string DefaultIbmPath = @"C:\Program Files\IBM\Personal Communications";
        private const string PcswsExecutableName = "pcsws.exe";

        public static void OpenWs(string wsFilePath, string ibmPath = null)
        {
            if (string.IsNullOrWhiteSpace(wsFilePath))
            {
                throw new ArgumentException("WS file path is required.", nameof(wsFilePath));
            }

            var absoluteWsPath = Path.GetFullPath(wsFilePath.Trim());
            if (!File.Exists(absoluteWsPath))
            {
                throw new FileNotFoundException("WS file was not found.", absoluteWsPath);
            }

            var pcswsPath = ResolvePcswsPath(ibmPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = pcswsPath,
                Arguments = QuoteCommandLineArgument(absoluteWsPath),
                UseShellExecute = false
            };

            if (Process.Start(startInfo) == null)
            {
                throw new InvalidOperationException("Failed to start PCSWS: " + pcswsPath);
            }
        }

        private static string ResolvePcswsPath(string ibmPath)
        {
            var rootPath = string.IsNullOrWhiteSpace(ibmPath) ? DefaultIbmPath : ibmPath.Trim();
            var absolutePath = Path.GetFullPath(rootPath);

            if (File.Exists(absolutePath))
            {
                return absolutePath;
            }

            if (!Directory.Exists(absolutePath))
            {
                throw new FileNotFoundException(
                    "IBM path was not found as a file or directory: " + absolutePath,
                    absolutePath);
            }

            var directPath = Path.GetFullPath(Path.Combine(absolutePath, PcswsExecutableName));
            if (File.Exists(directPath))
            {
                return directPath;
            }

            foreach (var candidate in Directory.EnumerateFiles(absolutePath, PcswsExecutableName, SearchOption.AllDirectories))
            {
                return Path.GetFullPath(candidate);
            }

            throw new FileNotFoundException(
                "PCSWS executable was not found under IBM path: " + absolutePath,
                directPath);
        }

        private static string QuoteCommandLineArgument(string value)
        {
            if (value.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0)
            {
                return "\"" + value.Replace("\"", "\\\"") + "\"";
            }

            return value;
        }
    }
}
