using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>Runs the x86 <see cref="IeComHostLocator.ComHostFileName"/> process from the x64 OpenRPA plugin.</summary>
    public static class IeComHostRunner
    {
        /// <summary>
        /// Start Trident IE via ComHost (COM). Does not wait for a window; use <see cref="EmbeddedIExplore.Connect"/> / Find Window.
        /// When <paramref name="url"/> is null or empty, only makes IE visible without navigation.
        /// </summary>
        public static void Launch(string url, out string methodUsed)
        {
            methodUsed = null;

            var hostExe = IeComHostLocator.ResolveComHostPath();
            var workDir = IeComHostLocator.ResolveComHostWorkingDirectory();
            var args = BuildArguments(url);

            var psi = new ProcessStartInfo
            {
                FileName = hostExe,
                Arguments = args,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(psi))
            {
                if (process == null)
                    throw new InvalidOperationException("Failed to start " + hostExe);

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                    throw new InvalidOperationException(
                        "IExplore.ComHost exited with code " + process.ExitCode + ".\n" + detail.Trim());
                }

                ParseSuccessLine(stdout, out methodUsed);
            }
        }

        private static string BuildArguments(string url)
        {
            var sb = new StringBuilder();
            sb.Append("launch");
            if (!string.IsNullOrWhiteSpace(url))
                sb.Append(' ').Append(Quote(url.Trim()));
            return sb.ToString();
        }

        private static string Quote(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";

        private static void ParseSuccessLine(string stdout, out string methodUsed)
        {
            methodUsed = null;

            if (string.IsNullOrWhiteSpace(stdout))
                throw new InvalidOperationException("IExplore.ComHost returned no output.");

            foreach (var rawLine in stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (!line.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var part in line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (part.StartsWith("method=", StringComparison.OrdinalIgnoreCase))
                        methodUsed = part.Substring("method=".Length);
                }

                if (!string.IsNullOrEmpty(methodUsed))
                    return;
            }

            throw new InvalidOperationException("IExplore.ComHost did not return OK line with method. Output:\n" + stdout);
        }
    }
}
