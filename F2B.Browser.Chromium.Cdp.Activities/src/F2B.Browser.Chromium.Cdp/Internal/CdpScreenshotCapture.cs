using System;
using System.Collections.Generic;
using System.IO;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpScreenshotCapture
    {
        internal static byte[] CaptureTab(CdpTab tab, bool fullPage)
        {
            if (tab == null)
            {
                throw new ArgumentNullException("tab");
            }

            var session = tab.GetSession();
            Dictionary<string, object> parameters;
            if (fullPage)
            {
                var size = tab.Rect.Size;
                if (size.Item1 <= 0 || size.Item2 <= 0)
                {
                    throw new BrowserException("Unable to capture full-page screenshot because page size is zero.");
                }

                parameters = BuildClipParameters(0, 0, size.Item1, size.Item2, true);
            }
            else
            {
                parameters = BuildViewportParameters();
            }

            return Capture(session, parameters);
        }

        internal static byte[] CaptureElement(CdpElement element, bool scrollIntoView)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            if (scrollIntoView)
            {
                element.ScrollToSee();
            }

            var location = element.Rect.Location;
            var size = element.Rect.Size;
            if (size.Item1 <= 0 || size.Item2 <= 0)
            {
                throw new BrowserException("Unable to capture element screenshot because element size is zero.");
            }

            var parameters = BuildClipParameters(
                location.Item1,
                location.Item2,
                size.Item1,
                size.Item2,
                true);

            return Capture(element.Tab.GetSession(), parameters);
        }

        internal static void SaveToFile(byte[] imageBytes, string path)
        {
            if (imageBytes == null)
            {
                throw new ArgumentNullException("imageBytes");
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException("path");
            }

            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllBytes(path, imageBytes);
        }

        private static byte[] Capture(CdpTabSession session, Dictionary<string, object> parameters)
        {
            var result = session.Send("Page.captureScreenshot", parameters);
            var data = CdpValueConverter.GetString(result, "data");
            if (string.IsNullOrEmpty(data))
            {
                throw new BrowserException("Page.captureScreenshot returned empty data.");
            }

            try
            {
                return Convert.FromBase64String(data);
            }
            catch (FormatException ex)
            {
                throw new BrowserException("Page.captureScreenshot returned invalid base64 data.", ex);
            }
        }

        private static Dictionary<string, object> BuildViewportParameters()
        {
            return new Dictionary<string, object>
            {
                { "format", "png" }
            };
        }

        private static Dictionary<string, object> BuildClipParameters(
            int x,
            int y,
            int width,
            int height,
            bool captureBeyondViewport)
        {
            return new Dictionary<string, object>
            {
                { "format", "png" },
                { "captureBeyondViewport", captureBeyondViewport },
                {
                    "clip", new Dictionary<string, object>
                    {
                        { "x", x },
                        { "y", y },
                        { "width", width },
                        { "height", height },
                        { "scale", 1 }
                    }
                }
            };
        }
    }
}
