using System;
using System.IO;

namespace F2B.Browser.Chromium.Bridge
{
    internal static class BridgeScreenshotHelper
    {
        public static void SaveDataUrl(string dataUrl, string path)
        {
            if (string.IsNullOrWhiteSpace(dataUrl))
                throw new InvalidOperationException("Screenshot dataUrl is empty.");

            if (string.IsNullOrWhiteSpace(path))
                return;

            var commaIndex = dataUrl.IndexOf(',');
            if (commaIndex < 0 || !dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Screenshot dataUrl format is invalid.");

            var base64 = dataUrl.Substring(commaIndex + 1);
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            File.WriteAllBytes(path, Convert.FromBase64String(base64));
        }
    }
}
