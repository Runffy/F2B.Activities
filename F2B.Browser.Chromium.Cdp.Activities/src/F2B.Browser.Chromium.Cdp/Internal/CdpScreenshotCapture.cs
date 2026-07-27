using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpScreenshotCapture
    {
        private struct ClipRect
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
        }

        internal static byte[] CaptureTab(CdpTab tab, bool fullPage)
        {
            if (tab == null)
            {
                throw new ArgumentNullException("tab");
            }

            var session = tab.GetSession();
            if (!fullPage)
            {
                return Capture(session, BuildViewportParameters());
            }

            var expanded = false;
            try
            {
                ClipRect clip;
                if (TryExpandMainScrollable(session, out clip))
                {
                    expanded = true;
                    Thread.Sleep(50);
                    return Capture(session, BuildClipParameters(clip.X, clip.Y, clip.Width, clip.Height, true));
                }

                var size = tab.Rect.Size;
                if (size.Item1 <= 0 || size.Item2 <= 0)
                {
                    throw new BrowserException("Unable to capture full-page screenshot because page size is zero.");
                }

                return Capture(session, BuildClipParameters(0, 0, size.Item1, size.Item2, true));
            }
            finally
            {
                if (expanded)
                {
                    RestoreExpandedStyles(session);
                }
            }
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

            var session = element.Tab.GetSession();
            var expanded = false;
            try
            {
                ClipRect clip;
                if (TryExpandElement(element, out clip))
                {
                    expanded = true;
                    Thread.Sleep(50);
                    return Capture(session, BuildClipParameters(clip.X, clip.Y, clip.Width, clip.Height, true));
                }

                var location = element.Rect.Location;
                var size = element.Rect.Size;
                if (size.Item1 <= 0 || size.Item2 <= 0)
                {
                    throw new BrowserException("Unable to capture element screenshot because element size is zero.");
                }

                return Capture(
                    session,
                    BuildClipParameters(location.Item1, location.Item2, size.Item1, size.Item2, true));
            }
            finally
            {
                if (expanded)
                {
                    RestoreExpandedStyles(session);
                }
            }
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

        private static bool TryExpandElement(CdpElement element, out ClipRect clip)
        {
            clip = default(ClipRect);
            var raw = element.RunJs(CdpScreenshotScripts.ExpandElementAndAncestors);
            return TryParseClip(raw, out clip);
        }

        private static bool TryExpandMainScrollable(CdpTabSession session, out ClipRect clip)
        {
            clip = default(ClipRect);
            var raw = session.RunJs(CdpScreenshotScripts.FindMainScrollableExpandAndMeasure);
            return TryParseClip(raw, out clip);
        }

        private static void RestoreExpandedStyles(CdpTabSession session)
        {
            try
            {
                session.RunJs(CdpScreenshotScripts.RestoreExpandedStyles);
            }
            catch (BrowserException)
            {
                // Best-effort restore; capture result is more important than style cleanup.
            }
        }

        private static bool TryParseClip(object raw, out ClipRect clip)
        {
            clip = default(ClipRect);
            var text = raw == null ? null : Convert.ToString(raw, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                return false;
            }

            int x, y, w, h;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out w) ||
                !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out h))
            {
                return false;
            }

            if (w <= 0 || h <= 0)
            {
                return false;
            }

            clip = new ClipRect { X = x, Y = y, Width = w, Height = h };
            return true;
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
