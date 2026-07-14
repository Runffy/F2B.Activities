using System;
using System.Collections.Generic;
using System.Linq;
using F2B.Browser.Chromium.Cdp.Internal;
using F2B.Browser.Chromium.Cdp.Selectors;

namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// A document context for an iframe/frame: tab-like for its document, element-like for the host node.
    /// </summary>
    public sealed class CdpFrame : CdpBase
    {
        private readonly CdpTab _tab;

        internal CdpFrame(
            CdpTab tab,
            string frameId,
            CdpElement hostElement,
            CdpBase parent,
            IList<SelectorLevel> frameLevelsFromTab = null)
        {
            if (tab == null)
            {
                throw new ArgumentNullException("tab");
            }

            if (string.IsNullOrEmpty(frameId))
            {
                throw new ArgumentException("frameId is required.", "frameId");
            }

            _tab = tab;
            FrameId = frameId;
            Element = hostElement;
            Parent = parent ?? tab;
            FrameLevelsFromTab = frameLevelsFromTab == null
                ? new List<SelectorLevel>()
                : new List<SelectorLevel>(frameLevelsFromTab);
        }

        public override CdpTab Tab
        {
            get { return _tab; }
        }

        /// <summary>CDP frame id.</summary>
        public string FrameId { get; private set; }

        /// <summary>Host iframe/frame element in the parent document.</summary>
        public CdpElement Element { get; private set; }

        /// <summary>Parent tab or outer frame.</summary>
        public CdpBase Parent { get; private set; }

        /// <summary>Frame selector levels from the tab root used to reach this frame.</summary>
        internal IList<SelectorLevel> FrameLevelsFromTab { get; private set; }

        public string Url
        {
            get { return Convert.ToString(EvaluateInFrame("location.href") ?? string.Empty); }
        }

        public string Title
        {
            get { return Convert.ToString(EvaluateInFrame("document.title") ?? string.Empty); }
        }

        public string Html
        {
            get { return Convert.ToString(EvaluateInFrame("document.documentElement ? document.documentElement.outerHTML : ''") ?? string.Empty); }
        }

        public override object RunJs(
            string script,
            object[] args = null,
            bool asExpression = false,
            bool isAsync = false,
            int timeoutMs = 30000)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                throw new ArgumentNullException("script");
            }

            if (isAsync)
            {
                var scriptCopy = script;
                var argsCopy = args;
                var asExprCopy = asExpression;
                System.Threading.ThreadPool.QueueUserWorkItem(_ => RunJsSync(scriptCopy, argsCopy, asExprCopy));
                return null;
            }

            return RunJsSync(script, args, asExpression);
        }

        public override CdpElement FindElement(string selectorXml, int timeoutMs = 15000, bool throwException = true)
        {
            return SelectorElementFinder.FindElement(this, selectorXml, timeoutMs, throwException);
        }

        public override CdpElement[] FindElements(string selectorXml)
        {
            return SelectorElementFinder.FindElements(this, selectorXml).ToArray();
        }

        public override bool ElementExists(string selectorXml)
        {
            return SelectorElementFinder.TryFindElement(this, selectorXml);
        }

        public override CdpFrame FindFrame(string selectorXml, int timeoutMs = 15000, bool throwException = true)
        {
            return CdpFrameResolver.FindFrame(_tab, this, null, selectorXml, timeoutMs, throwException);
        }

        public void WaitForDocumentComplete(int timeoutMs = 15000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            do
            {
                var ready = Convert.ToString(EvaluateInFrame("document.readyState") ?? string.Empty);
                if (string.Equals(ready, "complete", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                System.Threading.Thread.Sleep(50);
            }
            while (DateTime.UtcNow < deadline);

            throw new Exceptions.BrowserException(
                string.Format("Frame document did not reach readyState complete within {0} ms.", timeoutMs));
        }

        public void Navigate(string url)
        {
            if (url == null)
            {
                throw new ArgumentNullException("url");
            }

            EvaluateInFrame("location.href = " + ToJsString(url));
        }

        public void Refresh(bool ignoreCache = false)
        {
            if (ignoreCache)
            {
                EvaluateInFrame("location.reload(true)");
            }
            else
            {
                EvaluateInFrame("location.reload()");
            }
        }

        public void Back(int steps = 1)
        {
            var count = Math.Max(1, steps);
            EvaluateInFrame("history.go(-" + count + ")");
        }

        public void Forward(int steps = 1)
        {
            var count = Math.Max(1, steps);
            EvaluateInFrame("history.go(" + count + ")");
        }

        public void Scroll(CdpScrollDirection direction = CdpScrollDirection.Down, int pixel = 300)
        {
            var amount = Math.Max(0, pixel);
            string script;
            switch (direction)
            {
                case CdpScrollDirection.Up:
                    script = "window.scrollBy(0, -" + amount + ");";
                    break;
                case CdpScrollDirection.Left:
                    script = "window.scrollBy(-" + amount + ", 0);";
                    break;
                case CdpScrollDirection.Right:
                    script = "window.scrollBy(" + amount + ", 0);";
                    break;
                default:
                    script = "window.scrollBy(0, " + amount + ");";
                    break;
            }

            RunJs(script);
        }

        public byte[] GetScreenshot(bool fullPage = false)
        {
            // Capture host element region; full-page frame capture falls back to host element.
            if (Element != null)
            {
                return Element.GetScreenshot(scrollIntoView: true);
            }

            return _tab.GetScreenshot(fullPage);
        }

        public void SaveScreenshot(string path, bool fullPage = false)
        {
            CdpScreenshotCapture.SaveToFile(GetScreenshot(fullPage), path);
        }

        // --- Element-like API (delegates to host Element) ---

        public string Tag
        {
            get { return RequireElement().Tag; }
        }

        public string Attr(string name)
        {
            return RequireElement().Attr(name);
        }

        public string Property(string name)
        {
            return RequireElement().Property(name);
        }

        public string Style(string style, string pseudoElement = null)
        {
            return RequireElement().Style(style, pseudoElement);
        }

        public CdpElement[] Children()
        {
            return RequireElement().Children();
        }

        public void Click(
            CdpMouseButton button = CdpMouseButton.Left,
            CdpInteractionMethod method = CdpInteractionMethod.Simulate,
            int count = 1)
        {
            RequireElement().Click(button, method, count);
        }

        public void Input(object value, bool clear = false, CdpInteractionMethod method = CdpInteractionMethod.Simulate)
        {
            RequireElement().Input(value, clear, method);
        }

        public void SendKeys(params CdpKey[] keys)
        {
            RequireElement().SendKeys(keys);
        }

        public void SendKeys(IList<CdpKey> keys)
        {
            RequireElement().SendKeys(keys);
        }

        public void Focus()
        {
            RequireElement().Focus();
        }

        public void Drag(int offsetX = 0, int offsetY = 0, double duration = 0.5)
        {
            RequireElement().Drag(offsetX, offsetY, duration);
        }

        public void DragToElement(CdpElement targetElement, double duration = 0.5)
        {
            RequireElement().DragToElement(targetElement, duration);
        }

        public void DragToLocation(Tuple<int, int> targetLocation, double duration = 0.5)
        {
            RequireElement().DragToLocation(targetLocation, duration);
        }

        public void Hover(int? offsetX = null, int? offsetY = null)
        {
            RequireElement().Hover(offsetX, offsetY);
        }

        public void SetAttr(string name, string value)
        {
            RequireElement().SetAttr(name, value);
        }

        public void SetProperty(string name, string value)
        {
            RequireElement().SetProperty(name, value);
        }

        public void SetStyle(string name, string value)
        {
            RequireElement().SetStyle(name, value);
        }

        public void SetValue(string value)
        {
            RequireElement().SetValue(value);
        }

        public void RemoveAttr(string name)
        {
            RequireElement().RemoveAttr(name);
        }

        public void Check(bool uncheck = false, CdpInteractionMethod method = CdpInteractionMethod.Simulate)
        {
            RequireElement().Check(uncheck, method);
        }

        public override string ToString()
        {
            return string.Format("CdpFrame(FrameId={0}, Url={1})", FrameId, Url);
        }

        internal CdpDomContext CreateDomContext()
        {
            return CdpDomContext.ForFrameId(_tab, _tab.GetSession(), FrameId);
        }

        private CdpElement RequireElement()
        {
            if (Element == null)
            {
                throw new InvalidOperationException("Host frame element is not available.");
            }

            return Element;
        }

        private object EvaluateInFrame(string expression)
        {
            using (var context = CreateDomContext())
            {
                return context.Evaluate(expression);
            }
        }

        private static string ToJsString(string value)
        {
            return "'" + (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n") + "'";
        }

        private object RunJsSync(string script, object[] args, bool asExpression)
        {
            using (var context = CreateDomContext())
            {
                if (asExpression)
                {
                    return context.Evaluate(script);
                }

                var serializer = new CdpJsonSerializer();
                var argsLiteral = serializer.Serialize(args ?? new object[0]);
                var wrapped = CdpJsScript.WrapAsFunction(script);
                var expression = "(" + wrapped + ").apply(document, " + argsLiteral + ")";
                return context.Evaluate(expression);
            }
        }
    }
}
