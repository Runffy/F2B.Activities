using System;
using System.IO;

namespace F2B.Browser.Chromium.Cdp.Inspector.Services
{
    /// <summary>
    /// Reserved hooks for a future OpenRPA designer “…” launch path.
    /// Current release is a standalone tool (copy/paste Selector XML).
    /// Supported placeholders:
    /// <list type="bullet">
    /// <item><description><c>--selector-file &lt;path&gt;</c> — when set on process exit, write final SelectorXml to the file (not yet wired from UI Apply).</description></item>
    /// <item><description>stdout — future: emit SelectorXml and exit 0 on confirm.</description></item>
    /// </list>
    /// </summary>
    internal static class DesignerIntegrationStub
    {
        public static string SelectorOutputFile { get; private set; }

        public static void ParseStartupArgs(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return;
            }

            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--selector-file", StringComparison.OrdinalIgnoreCase) &&
                    i + 1 < args.Length)
                {
                    SelectorOutputFile = args[i + 1];
                    i++;
                }
            }
        }

        /// <summary>
        /// Best-effort write of selector XML for future designer round-trip.
        /// </summary>
        public static void TryWriteSelectorFile(string selectorXml)
        {
            if (string.IsNullOrWhiteSpace(SelectorOutputFile))
            {
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(SelectorOutputFile);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(SelectorOutputFile, selectorXml ?? string.Empty);
            }
            catch
            {
                // Reserved path — ignore IO failures in standalone mode.
            }
        }
    }
}
