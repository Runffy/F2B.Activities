using System;
using System.Globalization;
using F2B.Browser.IExplore.Com;

namespace IExplore.ComHost
{
    /// <summary>32-bit STA helper: starts Trident IE via COM for the x64 OpenRPA plugin.</summary>
    internal static class Program
    {
        private const int DefaultTimeoutMs = 45000;
        private const string DefaultTitlePart = "IExplore Test Host";

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    PrintUsage();
                    return 2;
                }

                if (string.Equals(args[0], "dom", StringComparison.OrdinalIgnoreCase))
                    return RunDom(args);

                if (!string.Equals(args[0], "launch", StringComparison.OrdinalIgnoreCase))
                {
                    PrintUsage();
                    return 2;
                }

                return RunLaunch(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static int RunLaunch(string[] args)
        {
            var url = GetRequiredArg(args, 1, "url");
            var timeoutMs = GetOptionalInt(args, "--timeout", DefaultTimeoutMs);
            var titlePart = GetOptionalString(args, "--title", DefaultTitlePart);
            var urlContains = GetOptionalString(args, "--url-contains", null);

            IeSecurityConfigurator.ApplyAutomationPolicy();
            HostIeLauncher.Start(url, out var method);

            var waitUrl = url;
            if (!string.IsNullOrWhiteSpace(urlContains)
                && (url ?? "").IndexOf(urlContains, StringComparison.OrdinalIgnoreCase) < 0)
            {
                waitUrl = "http://127.0.0.1/" + urlContains.TrimStart('/');
            }

            var hwnd = HostIeLauncher.WaitForIeBrowserWindow(waitUrl, titlePart, timeoutMs);

            if (hwnd == 0)
            {
                Console.Error.WriteLine(
                    "IE window not found within " + timeoutMs + " ms (title contains \"" + titlePart + "\").");
                return 1;
            }

            Console.WriteLine("OK method=" + method + " hwnd=" + hwnd.ToString(CultureInfo.InvariantCulture));
            return 0;
        }

        private static int RunDom(string[] args)
        {
            if (args.Length < 4)
                throw new ArgumentException("Usage: dom <hwnd> <request.json> <response.json>");

            var hwnd = int.Parse(args[1], CultureInfo.InvariantCulture);
            HostDomService.Execute(hwnd, args[2], args[3]);
            Console.WriteLine("OK");
            return 0;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("IExplore.ComHost.exe launch <url> [--timeout ms] [--title text] [--url-contains text]");
            Console.Error.WriteLine("IExplore.ComHost.exe dom <hwnd> <request.json> <response.json>");
        }

        private static string GetRequiredArg(string[] args, int index, string name)
        {
            if (args.Length <= index || string.IsNullOrWhiteSpace(args[index]))
                throw new ArgumentException("Missing " + name + ".");
            return args[index];
        }

        private static int GetOptionalInt(string[] args, string flag, int defaultValue)
        {
            var value = GetOptionalString(args, flag, null);
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            throw new ArgumentException("Invalid " + flag + ": " + value);
        }

        private static string GetOptionalString(string[] args, string flag, string defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return defaultValue;
        }
    }
}
