using System;
using System.Collections.Generic;
using F2B.Browser.Chromium.Cdp.Internal;

namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// A DOM element located within a tab.
    /// </summary>
    public sealed class CdpElement : CdpBase
    {
        private CdpElementContext _context;

        private readonly CdpTab _tab;

        internal CdpElement(CdpTab tab, string tag, int backendNodeId, int nodeId, string objectId)
        {
            _tab = tab;
            Tag = tag ?? string.Empty;
            BackendNodeId = backendNodeId;
            NodeId = nodeId;
            ObjectId = objectId ?? string.Empty;
        }

        public override CdpTab Tab
        {
            get { return _tab; }
        }

        /// <summary>HTML tag name in lower case.</summary>
        public string Tag { get; internal set; }

        internal int BackendNodeId { get; set; }

        internal int NodeId { get; set; }

        internal string ObjectId { get; set; }

        internal CdpElementContext Context
        {
            get { return _context ?? (_context = new CdpElementContext(this)); }
        }

        public string Html
        {
            get { return Context.GetHtml(); }
        }

        public string InnerHtml
        {
            get { return Context.GetInnerHtml(); }
        }

        public string Text
        {
            get { return Context.GetText(); }
        }

        public string RawText
        {
            get { return Context.GetRawText(); }
        }

        public string InnerText
        {
            get { return Context.GetInnerText(); }
        }

        public Dictionary<string, string> Attrs
        {
            get { return Context.GetAttrs(); }
        }

        /// <summary>Readable DOM/JS properties as a string dictionary.</summary>
        public Dictionary<string, string> Properties
        {
            get { return Context.GetProperties(); }
        }

        public string Value
        {
            get { return Context.GetValue(); }
        }

        public string Link
        {
            get { return Context.GetLink(); }
        }

        public CdpElementPseudo Pseudo
        {
            get { return Context.Pseudo; }
        }

        public int ChildCount
        {
            get { return Context.GetChildCount(); }
        }

        public CdpElementRect Rect
        {
            get { return Context.Rect; }
        }

        public string Xpath
        {
            get { return Context.GetXpath(); }
        }

        public string CssSelector
        {
            get { return Context.GetCssSelector(); }
        }

        public CdpElementStates States
        {
            get { return Context.States; }
        }

        public string Attr(string name)
        {
            return Context.GetAttr(name);
        }

        public string Property(string name)
        {
            return Context.GetProperty(name);
        }

        public string Style(string style, string pseudoElement = null)
        {
            return Context.GetStyle(style, pseudoElement);
        }

        public CdpElement[] Children()
        {
            return Context.GetChildren();
        }

        public override object RunJs(
            string script,
            object[] args = null,
            bool asExpression = false,
            bool isAsync = false,
            int timeoutMs = 30000)
        {
            return Context.RunJs(script, args, asExpression, isAsync, timeoutMs);
        }

        /// <summary>Converts an iframe/frame element into a <see cref="CdpFrame"/> context.</summary>
        public CdpFrame AsFrame()
        {
            return CdpFrameResolver.FromHostElement(this, Tab);
        }

        public void Click(
            CdpMouseButton button = CdpMouseButton.Left,
            CdpInteractionMethod method = CdpInteractionMethod.Simulate,
            int count = 1)
        {
            CdpElementActions.Click(this, button, method, count);
        }

        public void ClickToUpload(
            IEnumerable<string> filePaths,
            CdpMouseButton button = CdpMouseButton.Left,
            CdpInteractionMethod method = CdpInteractionMethod.Simulate,
            int count = 1)
        {
            CdpElementActions.ClickToUpload(this, filePaths, button, method, count);
        }

        public CdpDownloadResult ClickToDownload(
            string savePath = null,
            string rename = null,
            string suffix = null,
            bool newTab = true,
            CdpLocalFileExistsAction localFileExists = CdpLocalFileExistsAction.Overwrite,
            CdpMouseButton button = CdpMouseButton.Left,
            CdpInteractionMethod method = CdpInteractionMethod.Simulate,
            int count = 1,
            int timeoutMs = 30000)
        {
            return CdpElementActions.ClickToDownload(
                this,
                savePath,
                rename,
                suffix,
                newTab,
                localFileExists,
                button,
                method,
                count,
                timeoutMs);
        }

        public CdpTab ClickForNewTab(
            CdpMouseButton button = CdpMouseButton.Left,
            CdpInteractionMethod method = CdpInteractionMethod.Simulate,
            int count = 1,
            int timeoutMs = 3000)
        {
            return CdpElementActions.ClickForNewTab(this, button, method, count, timeoutMs);
        }

        public void Clear(CdpInteractionMethod method = CdpInteractionMethod.Simulate)
        {
            CdpElementActions.Clear(this, method);
        }

        public void Input(
            object value,
            bool clear = false,
            CdpInteractionMethod method = CdpInteractionMethod.Simulate)
        {
            CdpElementActions.Input(this, value, clear, method);
        }

        /// <summary>
        /// Sends special keys or key chords to the element. Use <see cref="CdpKey"/> constants only.
        /// </summary>
        public void SendKeys(params CdpKey[] keys)
        {
            CdpElementActions.SendKeys(this, keys);
        }

        /// <summary>
        /// Sends special keys or key chords to the element. Use <see cref="CdpKey"/> constants only.
        /// </summary>
        public void SendKeys(IList<CdpKey> keys)
        {
            CdpElementActions.SendKeys(this, keys == null ? new CdpKey[0] : ToArray(keys));
        }

        public void Focus()
        {
            CdpElementActions.Focus(this);
        }

        public void Drag(int offsetX = 0, int offsetY = 0, double duration = 0.5)
        {
            CdpElementActions.Drag(this, offsetX, offsetY, duration);
        }

        public void DragToElement(CdpElement targetElement, double duration = 0.5)
        {
            CdpElementActions.DragToElement(this, targetElement, duration);
        }

        public void DragToLocation(Tuple<int, int> targetLocation, double duration = 0.5)
        {
            CdpElementActions.DragToLocation(this, targetLocation, duration);
        }

        public void Hover(int? offsetX = null, int? offsetY = null)
        {
            CdpElementActions.Hover(this, offsetX, offsetY);
        }

        public void SetInnerHtml(string html)
        {
            CdpElementActions.SetInnerHtml(this, html);
        }

        public void SetStyle(string name, string value)
        {
            CdpElementActions.SetStyle(this, name, value);
        }

        public void SetAttr(string name, string value)
        {
            CdpElementActions.SetAttr(this, name, value);
        }

        public void SetValue(string value)
        {
            CdpElementActions.SetValue(this, value);
        }

        public void SetProperty(string name, string value)
        {
            CdpElementActions.SetProperty(this, name, value);
        }

        public void RemoveAttr(string name)
        {
            CdpElementActions.RemoveAttr(this, name);
        }

        public void Check(bool uncheck = false, CdpInteractionMethod method = CdpInteractionMethod.Simulate)
        {
            CdpElementActions.Check(this, uncheck, method);
        }

        public void Scroll(CdpScrollDirection direction = CdpScrollDirection.Down, int pixel = 300)
        {
            CdpElementActions.Scroll(this, direction, pixel);
        }

        public void ScrollToSee()
        {
            Context.ScrollToSee(false);
        }

        public void ScrollToCenter()
        {
            Context.ScrollToSee(true);
        }

        /// <summary>
        /// Captures a screenshot of this element's visible region.
        /// </summary>
        /// <param name="scrollIntoView">When true, scrolls the element into view before capture.</param>
        public byte[] GetScreenshot(bool scrollIntoView = true)
        {
            return CdpScreenshotCapture.CaptureElement(this, scrollIntoView);
        }

        /// <summary>
        /// Saves a screenshot of this element's visible region to the given file path.
        /// </summary>
        /// <param name="scrollIntoView">When true, scrolls the element into view before capture.</param>
        public void SaveScreenshot(string path, bool scrollIntoView = true)
        {
            CdpScreenshotCapture.SaveToFile(GetScreenshot(scrollIntoView), path);
        }

        /// <summary>
        /// Selects one or more options by text (default).
        /// </summary>
        public void Select(params object[] values)
        {
            CdpElementSelect.Select(this, CdpSelectBy.Text, values ?? new object[0], 15000);
        }

        /// <summary>
        /// Selects one or more options in a &lt;select&gt; element.
        /// </summary>
        public void Select(CdpSelectBy by, params object[] values)
        {
            CdpElementSelect.Select(this, by, values ?? new object[0], 15000);
        }

        /// <summary>
        /// Selects one or more options in a &lt;select&gt; element.
        /// </summary>
        public void Select(IEnumerable<object> values, CdpSelectBy by = CdpSelectBy.Text, int timeoutMs = 15000)
        {
            CdpElementSelect.Select(this, by, values == null ? new object[0] : ToObjectList(values), timeoutMs);
        }

        /// <summary>
        /// Unselects one or more options by text (default) in a multi-select list.
        /// </summary>
        public void Unselect(params object[] values)
        {
            CdpElementSelect.Unselect(this, CdpSelectBy.Text, values ?? new object[0], 15000);
        }

        /// <summary>
        /// Unselects one or more options in a multi-select &lt;select&gt; element.
        /// </summary>
        public void Unselect(CdpSelectBy by, params object[] values)
        {
            CdpElementSelect.Unselect(this, by, values ?? new object[0], 15000);
        }

        /// <summary>
        /// Unselects one or more options in a multi-select &lt;select&gt; element.
        /// </summary>
        public void Unselect(IEnumerable<object> values, CdpSelectBy by = CdpSelectBy.Text, int timeoutMs = 15000)
        {
            CdpElementSelect.Unselect(this, by, values == null ? new object[0] : ToObjectList(values), timeoutMs);
        }

        /// <summary>Selects all options in a multi-select &lt;select&gt; element.</summary>
        public void SelectAll()
        {
            CdpElementSelect.SelectAll(this);
        }

        /// <summary>Unselects all options in a multi-select &lt;select&gt; element.</summary>
        public void UnselectAll()
        {
            CdpElementSelect.UnselectAll(this);
        }

        /// <summary>Whether the element is a multi-select list.</summary>
        public bool IsMultiSelect
        {
            get { return CdpElementSelect.IsMultiSelect(this); }
        }

        /// <summary>All selectable option elements.</summary>
        public CdpElement[] SelectOptions
        {
            get { return CdpElementSelect.GetSelectOptions(this); }
        }

        /// <summary>Currently selected option elements.</summary>
        public CdpElement[] SelectedOptions
        {
            get { return CdpElementSelect.GetSelectedOptions(this); }
        }

        /// <summary>
        /// Finds the first child element matching selector XML within the given timeout.
        /// When timeout is 0, performs a single instantaneous lookup.
        /// </summary>
        /// <param name="throwException">When false, returns null instead of throwing if no element is found.</param>
        public override CdpElement FindElement(string selectorXml, int timeoutMs = 15000, bool throwException = true)
        {
            return SelectorElementFinder.FindElement(this, selectorXml, timeoutMs, throwException);
        }

        /// <summary>
        /// Finds all child elements matching selector XML using an instantaneous element snapshot.
        /// </summary>
        public override CdpElement[] FindElements(string selectorXml)
        {
            return SelectorElementFinder.FindElements(this, selectorXml);
        }

        public override bool ElementExists(string selectorXml)
        {
            try
            {
                return FindElement(selectorXml, 0, false) != null;
            }
            catch
            {
                return false;
            }
        }

        public override CdpFrame FindFrame(string selectorXml, int timeoutMs = 15000, bool throwException = true)
        {
            return CdpFrameResolver.FindFrame(Tab, null, this, selectorXml, timeoutMs, throwException);
        }

        public override string ToString()
        {
            return string.Format("CdpElement(Tag={0})", Tag);
        }

        private static CdpKey[] ToArray(IList<CdpKey> keys)
        {
            var array = new CdpKey[keys.Count];
            keys.CopyTo(array, 0);
            return array;
        }

        private static IList<object> ToObjectList(IEnumerable<object> values)
        {
            return values as IList<object> ?? new List<object>(values);
        }
    }
}
