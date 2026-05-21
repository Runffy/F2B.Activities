using System;
using F2B.Browser.IExplore.Com;

namespace F2B.Browser.IExplore
{
    /// <summary>
    /// Embedded IE window (x64 in-process MSHTML). Use <see cref="IELocator"/> for element + frame targeting.
    /// </summary>
    public sealed class EmbeddedIEWindow : ITridentDomHost
    {
        internal EmbeddedIEWindow(IntPtr topLevelHandle, IntPtr ieServerHandle, string className)
        {
            Handle = topLevelHandle;
            IeServerHandle = ieServerHandle;
            ClassName = className ?? string.Empty;
        }

        public IntPtr Handle { get; }
        public IntPtr IeServerHandle { get; }
        public string ClassName { get; }

        public string Title => Native.Win32Native.GetWindowTextString(Handle);

        public string Url => Dom(() =>
        {
            var shell = ShDocVwHelper.FindByHwnd((int)Handle.ToInt64());
            if (!string.IsNullOrEmpty(shell?.LocationUrl))
                return shell.LocationUrl;

            return HtmlDocumentHelper.ReadDocumentUrl(GetDocumentLocal());
        });

        public string Html => Dom(() => HtmlDocumentHelper.ReadOuterHtml(GetDocumentLocal()));

        internal string HtmlLocal => HtmlDocumentHelper.ReadOuterHtml(GetDocumentLocal());

        public IHTMLDocument2 GetDocument() => Dom(() => GetDocumentLocal());

        internal IHTMLDocument2 GetDocumentLocal()
        {
            if (!Native.Win32Native.IsWindow(IeServerHandle))
                throw new InvalidOperationException("IE server HWND is no longer valid.");

            return HtmlDocumentHelper.GetDocumentFromIeServer(IeServerHandle);
        }

        IHTMLDocument2 ITridentDomHost.GetMsHtmlDocument() => GetDocumentLocal();

        public void WaitForFrame(IELocator locator, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                var path = locator.ParseFramePath();
                if (path == null || path.Count == 0)
                    return;

                HtmlFrameHelper.WaitForFrameDocument(GetDocumentLocal(), path, timeout);
            });

        public void Refresh(int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() => HtmlFrameHelper.Refresh(GetDocumentLocal(), timeout));

        public void Navigate(string url) =>
            Dom(() =>
            {
                if (string.IsNullOrWhiteSpace(url))
                    throw new ArgumentException("URL is required.", nameof(url));

                NavigateLocal(url);
            });

        internal void NavigateLocal(string url)
        {
            try
            {
                dynamic doc = GetDocumentLocal();
                doc.parentWindow.navigate(url);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Navigate failed: " + ex.Message, ex);
            }
        }

        public IEHtmlElement FindElement(IELocator locator, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                return HtmlElementActions.FindElement(
                    this,
                    locator.ParseElement(),
                    locator.ParseFramePath(),
                    null,
                    timeout);
            });

        internal IEHtmlElement FindElementLocal(IELocator locator, int timeout = OperationDefaults.TimeoutMs) =>
            HtmlElementActions.FindElement(
                this,
                locator.ParseElement(),
                locator.ParseFramePath(),
                null,
                timeout);

        internal IEHtmlElement[] FindElementsLocal(IELocator locator, int timeout = OperationDefaults.TimeoutMs) =>
            HtmlElementActions.FindElements(
                this,
                locator.ParseElement(),
                locator.ParseFramePath(),
                null,
                timeout);

        public IEHtmlElement FindElement(
            IELocator locator,
            IEHtmlElement scope,
            int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                return HtmlElementActions.FindElement(
                    this,
                    locator.ParseElement(),
                    null,
                    scope,
                    timeout);
            });

        public bool ElementExists(IELocator locator) => ElementExists(locator, null);

        public bool ElementExists(IELocator locator, IEHtmlElement parent) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                return HtmlElementActions.ElementExists(
                    this,
                    locator.ParseElement(),
                    parent == null ? locator.ParseFramePath() : null,
                    parent);
            });

        internal bool ElementExistsLocal(IELocator locator) =>
            HtmlElementActions.ElementExists(this, locator.ParseElement(), locator.ParseFramePath(), null);

        public IEHtmlElement[] FindElements(IELocator locator, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                return HtmlElementActions.FindElements(
                    this,
                    locator.ParseElement(),
                    locator.ParseFramePath(),
                    null,
                    timeout);
            });

        public IEHtmlElement[] FindElements(
            IELocator locator,
            IEHtmlElement scope,
            int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                return HtmlElementActions.FindElements(
                    this,
                    locator.ParseElement(),
                    null,
                    scope,
                    timeout);
            });

        public ParallelFindElementResult ParallelFindElement(
            IELocator[] locators,
            int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locators == null)
                    throw new ArgumentNullException(nameof(locators));
                if (locators.Length == 0)
                    throw new ArgumentException("At least one locator is required.", nameof(locators));

                return HtmlElementActions.ParallelFindElement(this, locators, timeout);
            });

        public void Click(IELocator locator, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                HtmlElementActions.Click(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
            });

        public void Click(IEHtmlElement element, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() => HtmlElementActions.Click(this, element, timeout));

        public void DoubleClick(IELocator locator, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                HtmlElementActions.DoubleClick(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
            });

        public void DoubleClick(IEHtmlElement element, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() => HtmlElementActions.DoubleClick(this, element, timeout));

        public void Input(IELocator locator, int timeout = OperationDefaults.TimeoutMs) =>
            Input(locator, null, timeout);

        public void Input(IELocator locator, string value, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                var element = locator.ParseElement();
                if (value != null)
                    element[ElementLocatorKeys.InputText] = value;
                HtmlElementActions.Input(this, element, locator.ParseFramePath(), timeout);
            });

        public void Input(IEHtmlElement element, string value, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() => HtmlElementActions.Input(this, element, value, timeout));

        public void Check(IELocator locator, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                HtmlElementActions.Check(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
            });

        public void Check(IEHtmlElement element, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() => HtmlElementActions.Check(this, element, timeout));

        public void Uncheck(IELocator locator, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                HtmlElementActions.Uncheck(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
            });

        public void Uncheck(IEHtmlElement element, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() => HtmlElementActions.Uncheck(this, element, timeout));

        public bool IsChecked(IELocator locator, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                return HtmlElementActions.IsChecked(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
            });

        public bool IsChecked(IEHtmlElement element, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() => HtmlElementActions.IsChecked(this, element, timeout));

        public void Select(IELocator locator, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                HtmlElementActions.Select(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
            });

        public string GetText(IELocator locator, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                return HtmlElementActions.GetText(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
            });

        public string GetText(IEHtmlElement element, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() => HtmlElementActions.GetText(this, element, timeout));

        public string GetValue(IELocator locator, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                return HtmlElementActions.GetValue(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
            });

        public string GetValue(IEHtmlElement element, int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() => HtmlElementActions.GetValue(this, element, timeout));

        public string GetAttribute(
            IELocator locator,
            string attributeName,
            int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                return HtmlElementActions.GetAttribute(
                    this,
                    locator.ParseElement(),
                    attributeName,
                    locator.ParseFramePath(),
                    timeout);
            });

        public string GetAttribute(
            IEHtmlElement element,
            string attributeName,
            int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() => HtmlElementActions.GetAttribute(this, element, attributeName, timeout));

        public void SetAttribute(
            IELocator locator,
            string attributeName,
            string value,
            int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                HtmlElementActions.SetAttribute(
                    this,
                    locator.ParseElement(),
                    attributeName,
                    value,
                    locator.ParseFramePath(),
                    timeout);
            });

        public void SetAttribute(
            IEHtmlElement element,
            string attributeName,
            string value,
            int timeout = OperationDefaults.TimeoutMs) =>
            Dom(() => HtmlElementActions.SetAttribute(this, element, attributeName, value, timeout));

        public IeScriptResult ExecuteScript(
            string script,
            IELocator locator = null,
            IEHtmlElement target = null,
            string argsJson = null) =>
            Dom(() => ExecuteScriptLocal(script, locator, target, argsJson));

        internal IeScriptResult ExecuteScriptLocal(
            string script,
            IELocator locator = null,
            IEHtmlElement target = null,
            string argsJson = null)
        {
            var doc = HtmlElementActions.ResolveDocument(
                this,
                locator?.ParseFramePath(),
                OperationDefaults.TimeoutMs);

            object rawElement = target == null ? null : IEHtmlElement.Unwrap(target);
            var args = IeScriptExecute.ParseArgsJson(argsJson);
            var result = IeScriptExecute.Execute(doc, script, rawElement, args);
            return new IeScriptResult(result);
        }

        public IeScriptResult ExecuteScript(
            string script,
            IELocator locator,
            string argsJson,
            int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            var handle = FindElement(locator, timeout);
            return ExecuteScript(script, locator, handle, argsJson);
        }

        public void WireDemoHandlers() =>
            Dom(() =>
            {
                IeScriptHelper.EnsurePageHandlers(GetDocumentLocal(), "resetDemoCounters", "wireDemoHandlers");

                var goPath = new IELocator("{'id':'go'}", "[{'name':'f-actions'},{'name':'f-go'}]").ParseFramePath();
                if (goPath == null || goPath.Count == 0)
                    return;

                var segments = FramePathParse.Parse(goPath);
                var goDoc = HtmlFrameHelper.TryGetFrameDocument(GetDocument(), segments);
                if (goDoc != null)
                    IeScriptHelper.EnsurePageHandlers(goDoc, "wireDemoHandlers");
            });

        public override string ToString() =>
            $"EmbeddedIEWindow(Handle=0x{Handle.ToInt64():X}, Class={ClassName}, Title={Title})";

        private static void Dom(Action action) => StaInvoker.Invoke(action);

        private static T Dom<T>(Func<T> func) => StaInvoker.Invoke(func);
    }
}
