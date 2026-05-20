using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>Runs the x86 <see cref="IeComHostLocator.ComHostFileName"/> process from the x64 OpenRPA plugin.</summary>
    public static class IeComHostRunner
    {
        public const string DefaultTitlePart = "IExplore Test Host";

        /// <summary>
        /// Launch Trident IE via ComHost. Blocks until ComHost exits.
        /// </summary>
        public static void Launch(
            string url,
            out string methodUsed,
            out int hwnd,
            int timeoutMs = 45000,
            string titlePart = DefaultTitlePart,
            string urlContains = null)
        {
            methodUsed = null;
            hwnd = 0;

            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("Url is required.", nameof(url));

            var hostExe = IeComHostLocator.ResolveComHostPath();
            var workDir = IeComHostLocator.ResolveComHostWorkingDirectory();
            var args = BuildArguments(url, timeoutMs, titlePart, urlContains);

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

                ParseSuccessLine(stdout, out methodUsed, out hwnd);
            }
        }

        private static string BuildArguments(string url, int timeoutMs, string titlePart, string urlContains)
        {
            var sb = new StringBuilder();
            sb.Append("launch ");
            sb.Append(Quote(url));
            sb.Append(" --timeout ").Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(titlePart))
            {
                sb.Append(" --title ").Append(Quote(titlePart));
            }

            if (!string.IsNullOrWhiteSpace(urlContains))
            {
                sb.Append(" --url-contains ").Append(Quote(urlContains));
            }

            return sb.ToString();
        }

        private static string Quote(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";

        private static void ParseSuccessLine(string stdout, out string methodUsed, out int hwnd)
        {
            methodUsed = null;
            hwnd = 0;

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
                    else if (part.StartsWith("hwnd=", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(part.Substring("hwnd=".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out hwnd);
                }

                if (hwnd != 0)
                    return;
            }

            throw new InvalidOperationException("IExplore.ComHost did not return OK line with hwnd. Output:\n" + stdout);
        }
    }
}
