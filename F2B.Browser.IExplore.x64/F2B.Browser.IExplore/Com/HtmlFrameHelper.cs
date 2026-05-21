using System;
using System.Collections.Generic;
using System.Text;
using F2B.Browser.IExplore;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>Navigate nested IE <c>frames</c> / <c>iframes</c> using structured locators per segment.</summary>
    internal static class HtmlFrameHelper
    {
        public static IHTMLDocument2 WaitForFrameDocument(
            IHTMLDocument2 rootDocument,
            IList<IDictionary<string, object>> framePath,
            int timeoutMs)
        {
            if (rootDocument == null)
                throw new ArgumentNullException(nameof(rootDocument));

            var segments = FramePathParse.Parse(framePath);
            if (segments.Count == 0)
                return rootDocument;

            OperationTimeout.Validate(timeoutMs, nameof(timeoutMs));
            var pathDesc = DescribePath(segments);

            return OperationTimeout.WaitUntil(
                timeoutMs,
                () => TryGetFrameDocument(rootDocument, segments),
                () => new TimeoutException(
                    "Timed out after " + timeoutMs + " ms waiting for frame path: " + pathDesc));
        }

        public static IHTMLDocument2 TryGetFrameDocument(
            IHTMLDocument2 rootDocument,
            IList<ParsedFrameSegment> segments)
        {
            if (rootDocument == null)
                return null;
            if (segments == null || segments.Count == 0)
                return rootDocument;

            try
            {
                dynamic container = GetFrameContainer(rootDocument);
                if (container == null)
                    return null;

                foreach (var segment in segments)
                {
                    var frame = ResolveFrame(container, segment);
                    if (frame == null)
                        return null;

                    container = frame;
                }

                dynamic frameDoc = container.document;
                return frameDoc as IHTMLDocument2;
            }
            catch
            {
                return null;
            }
        }

        public static void Refresh(IHTMLDocument2 document, int timeoutMs)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            OperationTimeout.Validate(timeoutMs, nameof(timeoutMs));

            try
            {
                dynamic doc = document;
                dynamic win = doc.parentWindow;
                win.location.reload(true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Refresh failed: " + ex.Message, ex);
            }

            WaitForDocumentReady(document, timeoutMs);
        }

        public static void WaitForDocumentReady(IHTMLDocument2 document, int timeoutMs)
        {
            if (document == null)
                return;

            OperationTimeout.Validate(timeoutMs, nameof(timeoutMs));

            OperationTimeout.WaitUntil(
                timeoutMs,
                () => IsDocumentReady(document) ? document : null,
                () => new TimeoutException(
                    "Timed out after " + timeoutMs + " ms waiting for document readyState (complete/interactive)."));
        }

        public static bool IsDocumentReady(IHTMLDocument2 document)
        {
            var state = HtmlDocumentHelper.ReadReadyState(document);
            if (string.IsNullOrEmpty(state))
                return true;

            return state.Equals("complete", StringComparison.OrdinalIgnoreCase)
                || state.Equals("interactive", StringComparison.OrdinalIgnoreCase);
        }

        public static string DescribePath(IList<ParsedFrameSegment> segments)
        {
            if (segments == null || segments.Count == 0)
                return "(root)";

            var sb = new StringBuilder();
            for (int i = 0; i < segments.Count; i++)
            {
                if (i > 0)
                    sb.Append(" > ");

                var s = segments[i];
                if (!string.IsNullOrEmpty(s.Name))
                    sb.Append(FrameLocatorKeys.Name).Append('=').Append(s.Name);
                else if (!string.IsNullOrEmpty(s.Id))
                    sb.Append(FrameLocatorKeys.Id).Append('=').Append(s.Id);
                else if (s.Index.HasValue)
                    sb.Append(FrameLocatorKeys.Index).Append('=').Append(s.Index.Value);
                else if (!string.IsNullOrEmpty(s.SrcContains))
                    sb.Append(FrameLocatorKeys.Src).Append('~').Append(s.SrcContains);
            }

            return sb.ToString();
        }

        private static dynamic GetFrameContainer(IHTMLDocument2 document)
        {
            dynamic doc = document;
            return doc.parentWindow;
        }

        private static dynamic ResolveFrame(dynamic frameContainer, ParsedFrameSegment segment)
        {
            if (frameContainer == null || segment == null)
                return null;

            if (segment.Index.HasValue)
                return GetFrameByIndex(frameContainer, segment.Index.Value);

            if (!string.IsNullOrEmpty(segment.Name))
                return GetFrameByName(frameContainer, segment.Name);

            if (!string.IsNullOrEmpty(segment.Id))
                return GetFrameByElementId(frameContainer, segment.Id);

            if (!string.IsNullOrEmpty(segment.SrcContains))
                return GetFrameBySrcContains(frameContainer, segment.SrcContains);

            return null;
        }

        private static dynamic GetFrameByIndex(dynamic frameContainer, int index)
        {
            dynamic frames = frameContainer.frames;
            if (frames == null)
                return null;

            try { return frames.item(index); }
            catch
            {
                try { return frames[index]; }
                catch { return null; }
            }
        }

        private static dynamic GetFrameByName(dynamic frameContainer, string name)
        {
            dynamic frames = frameContainer.frames;
            if (frames == null)
                return null;

            try { return frames.item(name); }
            catch
            {
                try { return frames[name]; }
                catch { return null; }
            }
        }

        private static dynamic GetFrameByElementId(dynamic frameContainer, string id)
        {
            try
            {
                dynamic doc = frameContainer.document;
                if (doc == null)
                    return null;

                dynamic el = doc.getElementById(id);
                if (el == null)
                    return null;

                try
                {
                    dynamic win = el.contentWindow;
                    if (win != null)
                        return win;
                }
                catch { /* not an iframe */ }

                return el;
            }
            catch
            {
                return null;
            }
        }

        private static dynamic GetFrameBySrcContains(dynamic frameContainer, string srcContains)
        {
            dynamic frames = frameContainer.frames;
            if (frames == null)
                return null;

            try
            {
                int length = frames.length;
                for (int i = 0; i < length; i++)
                {
                    dynamic frame = null;
                    try { frame = frames.item(i); }
                    catch { continue; }

                    if (FrameMatchesSrc(frame, srcContains))
                        return frame;
                }
            }
            catch { /* ignore */ }

            return FindIframeBySrcAttribute(frameContainer, srcContains);
        }

        private static bool FrameMatchesSrc(dynamic frame, string srcContains)
        {
            if (frame == null)
                return false;

            try
            {
                dynamic doc = frame.document;
                string url = null;
                try { url = doc.url; }
                catch { /* ignore */ }

                if (!string.IsNullOrEmpty(url)
                    && url.IndexOf(srcContains, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            catch { /* ignore */ }

            try
            {
                dynamic el = frame.frameElement;
                if (el != null)
                {
                    string src = el.src;
                    if (!string.IsNullOrEmpty(src)
                        && src.IndexOf(srcContains, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            catch { /* ignore */ }

            return false;
        }

        private static dynamic FindIframeBySrcAttribute(dynamic frameContainer, string srcContains)
        {
            try
            {
                dynamic doc = frameContainer.document;
                if (doc == null)
                    return null;

                dynamic collection = doc.getElementsByTagName("iframe");
                if (collection == null)
                    return null;

                int length = collection.length;
                for (int i = 0; i < length; i++)
                {
                    dynamic el = collection.item(i);
                    if (el == null)
                        continue;

                    string src = null;
                    try { src = el.src; }
                    catch { continue; }

                    if (string.IsNullOrEmpty(src)
                        || src.IndexOf(srcContains, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    try
                    {
                        dynamic win = el.contentWindow;
                        if (win != null)
                            return win;
                    }
                    catch { /* ignore */ }
                }
            }
            catch { /* ignore */ }

            return null;
        }
    }
}
