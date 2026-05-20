using System;
using F2B.Browser.IExplore.Com;

namespace F2B.Browser.IExplore
{
    /// <summary>
    /// Embedded IE window. Use <see cref="IELocator"/> for element + frame targeting.
    /// <para>VB example:</para>
    /// <code>
    /// Dim loc As New IELocator("{ 'tag': 'button', 'type': 'submit' }", "[{'name':'f-actions'},{'name':'f-go'}]")
    /// win.Click(loc)
    /// </code>
    /// </summary>
    public sealed class EmbeddedIEWindow : ITridentDomHost
    {
        internal EmbeddedIEWindow(IntPtr topLevelHandle, IntPtr ieServerHandle, string className, bool? remoteDom = null)
        {
            Handle = topLevelHandle;
            IeServerHandle = ieServerHandle;
            ClassName = className ?? string.Empty;
            RemoteDomEnabled = remoteDom ?? Com.IeComHostDomBridge.IsRemoteDomRequired;
        }

        public IntPtr Handle { get; }
        public IntPtr IeServerHandle { get; }
        public string ClassName { get; }

        /// <summary>x64 OpenRPA: MSHTML runs in x86 ComHost.</summary>
        public bool RemoteDomEnabled { get; }

        public string Title => Native.Win32Native.GetWindowTextString(Handle);

        public string Url
        {
            get
            {
                if (RemoteDomEnabled)
                {
                    var resp = Com.IeComHostDomBridge.Execute(Handle, new Com.IeComHostDomRequest { Op = "url" });
                    Com.IeComHostDomBridge.EnsureOk(resp, "url");
                    return resp.StringResult ?? string.Empty;
                }

                var shell = Com.ShDocVwHelper.FindByHwnd((int)Handle.ToInt64());
                if (!string.IsNullOrEmpty(shell?.LocationUrl))
                    return shell.LocationUrl;

                return HtmlDocumentHelper.ReadDocumentUrl(GetDocument());
            }
        }

        public string Html
        {
            get
            {
                if (RemoteDomEnabled)
                {
                    var resp = Com.IeComHostDomBridge.Execute(Handle, new Com.IeComHostDomRequest { Op = "html" });
                    Com.IeComHostDomBridge.EnsureOk(resp, "html");
                    return resp.StringResult ?? string.Empty;
                }

                return HtmlDocumentHelper.ReadOuterHtml(GetDocument());
            }
        }

        internal string HtmlLocal => HtmlDocumentHelper.ReadOuterHtml(GetDocumentLocal());

        public IHTMLDocument2 GetDocument()
        {
            if (RemoteDomEnabled)
                throw new NotSupportedException(
                    "Direct MSHTML access is not available from the x64 OpenRPA plugin. Operations are proxied via IExplore.ComHost.exe.");

            return GetDocumentLocal();
        }

        internal IHTMLDocument2 GetDocumentLocal()
        {
            if (!Native.Win32Native.IsWindow(IeServerHandle))
                throw new InvalidOperationException("IE server HWND is no longer valid.");

            return HtmlDocumentHelper.GetDocumentFromIeServer(IeServerHandle);
        }

        IHTMLDocument2 ITridentDomHost.GetMsHtmlDocument() => GetDocumentLocal();

        /// <summary>Wait until <see cref="IELocator.FramePath"/> exists (<c>null</c> / <c>[]</c> = root, no-op).</summary>
        public void WaitForFrame(IELocator locator, int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            var path = locator.ParseFramePath();
            if (path == null || path.Count == 0)
                return;

            if (RemoteDomEnabled)
            {
                HtmlElementActionsRemote.Run(
                    this,
                    new Com.IeComHostDomRequest
                    {
                        Op = "waitframe",
                        ElementJson = locator.Element,
                        FramePathJson = locator.FramePath,
                        Timeout = timeout
                    },
                    "waitframe");
                return;
            }

            HtmlFrameHelper.WaitForFrameDocument(GetDocumentLocal(), path, timeout);
        }

        public void Refresh(int timeout = OperationDefaults.TimeoutMs)
        {
            if (RemoteDomEnabled)
            {
                HtmlElementActionsRemote.Run(this, new Com.IeComHostDomRequest { Op = "refresh", Timeout = timeout }, "refresh");
                return;
            }

            HtmlFrameHelper.Refresh(GetDocumentLocal(), timeout);
        }

        public void Navigate(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL is required.", nameof(url));

            if (RemoteDomEnabled)
            {
                var resp = Com.IeComHostDomBridge.Execute(Handle, new Com.IeComHostDomRequest
                {
                    Op = "navigate",
                    Url = url
                });
                Com.IeComHostDomBridge.EnsureOk(resp, "navigate");
                return;
            }

            NavigateLocal(url);
        }

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

        public IEHtmlElement FindElement(IELocator locator, int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            if (RemoteDomEnabled)
                return FindElementRemote(locator, timeout);

            return HtmlElementActions.FindElement(
                this,
                locator.ParseElement(),
                locator.ParseFramePath(),
                null,
                timeout);
        }

        internal IEHtmlElement FindElementLocal(IELocator locator, int timeout = OperationDefaults.TimeoutMs)
        {
            return HtmlElementActions.FindElement(
                this,
                locator.ParseElement(),
                locator.ParseFramePath(),
                null,
                timeout);
        }

        internal IEHtmlElement[] FindElementsLocal(IELocator locator, int timeout = OperationDefaults.TimeoutMs)
        {
            return HtmlElementActions.FindElements(
                this,
                locator.ParseElement(),
                locator.ParseFramePath(),
                null,
                timeout);
        }

        private IEHtmlElement FindElementRemote(IELocator locator, int timeout)
        {
            var resp = Com.IeComHostDomBridge.Execute(Handle, new Com.IeComHostDomRequest
            {
                Op = "find",
                ElementJson = locator.Element,
                FramePathJson = locator.FramePath,
                Timeout = timeout
            });
            Com.IeComHostDomBridge.EnsureOk(resp, "find");
            return IEHtmlElement.FromRemote(Handle.ToInt64(), locator.Element, locator.FramePath, null);
        }

        /// <summary>Find inside <paramref name="scope"/>; <see cref="IELocator.FramePath"/> is ignored.</summary>
        public IEHtmlElement FindElement(
            IELocator locator,
            IEHtmlElement scope,
            int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            if (RemoteDomEnabled)
            {
                if (scope == null || !scope.IsRemote)
                    throw new ArgumentException("Scoped find requires a remote parent element.", nameof(scope));

                var request = new Com.IeComHostDomRequest
                {
                    Op = "find",
                    ElementJson = locator.Element,
                    Timeout = timeout
                };
                Com.HostDomRequestResolver.CopyScopeFromParent(request, scope);
                var resp = Com.IeComHostDomBridge.Execute(Handle, request);
                Com.IeComHostDomBridge.EnsureOk(resp, "find");
                return IEHtmlElement.FromRemote(Handle.ToInt64(), locator.Element, null, null, scope);
            }

            return HtmlElementActions.FindElement(
                this,
                locator.ParseElement(),
                null,
                scope,
                timeout);
        }

        /// <summary>Instant check whether <paramref name="locator"/> matches an element (no wait).</summary>
        public bool ElementExists(IELocator locator) =>
            ElementExists(locator, null);

        /// <summary>Instant check under optional <paramref name="parent"/>; frame path on locator is ignored when parent is set.</summary>
        public bool ElementExists(IELocator locator, IEHtmlElement parent)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            if (RemoteDomEnabled)
            {
                var request = new Com.IeComHostDomRequest
                {
                    Op = "exists",
                    ElementJson = locator.Element,
                    FramePathJson = parent == null ? locator.FramePath : null
                };
                if (parent != null)
                    Com.HostDomRequestResolver.CopyScopeFromParent(request, parent);

                var resp = Com.IeComHostDomBridge.Execute(Handle, request);
                Com.IeComHostDomBridge.EnsureOk(resp, "exists");
                return resp.Found;
            }

            return HtmlElementActions.ElementExists(
                this,
                locator.ParseElement(),
                parent == null ? locator.ParseFramePath() : null,
                parent);
        }

        internal bool ElementExistsLocal(IELocator locator) =>
            HtmlElementActions.ElementExists(this, locator.ParseElement(), locator.ParseFramePath(), null);

        /// <summary>
        /// Find all elements matching <paramref name="locator"/> filters.
        /// <see cref="ElementLocatorKeys.Idx"/> in the locator JSON is ignored.
        /// </summary>
        public IEHtmlElement[] FindElements(IELocator locator, int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            if (RemoteDomEnabled)
            {
                var resp = Com.IeComHostDomBridge.Execute(Handle, new Com.IeComHostDomRequest
                {
                    Op = "findelements",
                    ElementJson = locator.Element,
                    FramePathJson = locator.FramePath,
                    Timeout = timeout
                });
                Com.IeComHostDomBridge.EnsureOk(resp, "findelements");
                var count = resp.IntResult ?? 0;
                var result = new IEHtmlElement[count];
                for (int i = 0; i < count; i++)
                {
                    result[i] = IEHtmlElement.FromRemote(Handle.ToInt64(), locator.Element, locator.FramePath, i);
                }

                return result;
            }

            return HtmlElementActions.FindElements(
                this,
                locator.ParseElement(),
                locator.ParseFramePath(),
                null,
                timeout);
        }

        /// <summary>
        /// Find all matches inside <paramref name="scope"/>; <see cref="IELocator.FramePath"/> is ignored.
        /// <see cref="ElementLocatorKeys.Idx"/> is ignored.
        /// </summary>
        public IEHtmlElement[] FindElements(
            IELocator locator,
            IEHtmlElement scope,
            int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            if (RemoteDomEnabled)
            {
                if (scope == null || !scope.IsRemote)
                    throw new ArgumentException("Scoped find requires a remote parent element.", nameof(scope));

                var request = new Com.IeComHostDomRequest
                {
                    Op = "findelements",
                    ElementJson = locator.Element,
                    Timeout = timeout
                };
                Com.HostDomRequestResolver.CopyScopeFromParent(request, scope);
                var resp = Com.IeComHostDomBridge.Execute(Handle, request);
                Com.IeComHostDomBridge.EnsureOk(resp, "findelements");
                var count = resp.IntResult ?? 0;
                var result = new IEHtmlElement[count];
                for (int i = 0; i < count; i++)
                    result[i] = IEHtmlElement.FromRemote(Handle.ToInt64(), locator.Element, null, i, scope);

                return result;
            }

            return HtmlElementActions.FindElements(
                this,
                locator.ParseElement(),
                null,
                scope,
                timeout);
        }

        /// <summary>
        /// Poll <paramref name="locators"/> in parallel; returns the index and element of the first match.
        /// All other polls stop as soon as one locator succeeds. Throws if none match within <paramref name="timeout"/>.
        /// </summary>
        public ParallelFindElementResult ParallelFindElement(
            IELocator[] locators,
            int timeout = OperationDefaults.TimeoutMs)
        {
            if (locators == null)
                throw new ArgumentNullException(nameof(locators));
            if (locators.Length == 0)
                throw new ArgumentException("At least one locator is required.", nameof(locators));

            if (RemoteDomEnabled)
            {
                var resp = Com.IeComHostDomBridge.Execute(Handle, new Com.IeComHostDomRequest
                {
                    Op = "parallelfind",
                    LocatorsJson = Com.IeComHostDomBridge.SerializeLocatorElements(locators),
                    FramePathJson = locators[0].FramePath,
                    Timeout = timeout
                });
                Com.IeComHostDomBridge.EnsureOk(resp, "parallelfind");
                var index = resp.ParallelIndex ?? 0;
                if (index < 0 || index >= locators.Length)
                    throw new InvalidOperationException("ComHost parallelfind returned invalid index: " + index);

                var winner = locators[index];
                return new ParallelFindElementResult(
                    index,
                    IEHtmlElement.FromRemote(Handle.ToInt64(), winner.Element, winner.FramePath, null));
            }

            return HtmlElementActions.ParallelFindElement(this, locators, timeout);
        }

        public void Click(IELocator locator, int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            if (RemoteDomEnabled)
            {
                HtmlElementActionsRemote.Run(
                    this,
                    HtmlElementActionsRemote.LocatorRequest(this, "click", locator, timeout),
                    "click");
                return;
            }

            HtmlElementActions.Click(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
        }

        public void Click(IEHtmlElement element, int timeout = OperationDefaults.TimeoutMs)
        {
            if (RemoteDomEnabled)
            {
                HtmlElementActionsRemote.Run(
                    this,
                    HtmlElementActionsRemote.ElementRequest(this, "click", element, timeout),
                    "click");
                return;
            }

            HtmlElementActions.Click(this, element, timeout);
        }

        public void DoubleClick(IELocator locator, int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            if (RemoteDomEnabled)
            {
                HtmlElementActionsRemote.Run(
                    this,
                    HtmlElementActionsRemote.LocatorRequest(this, "dblclick", locator, timeout),
                    "dblclick");
                return;
            }

            HtmlElementActions.DoubleClick(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
        }

        public void DoubleClick(IEHtmlElement element, int timeout = OperationDefaults.TimeoutMs)
        {
            if (RemoteDomEnabled)
            {
                HtmlElementActionsRemote.Run(
                    this,
                    HtmlElementActionsRemote.ElementRequest(this, "dblclick", element, timeout),
                    "dblclick");
                return;
            }

            HtmlElementActions.DoubleClick(this, element, timeout);
        }

        public void Input(IELocator locator, int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            if (RemoteDomEnabled)
            {
                HtmlElementActionsRemote.Run(
                    this,
                    HtmlElementActionsRemote.LocatorRequest(this, "input", locator, timeout),
                    "input");
                return;
            }

            HtmlElementActions.Input(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
        }

        public void Input(IEHtmlElement element, string value, int timeout = OperationDefaults.TimeoutMs)
        {
            if (RemoteDomEnabled)
            {
                var req = HtmlElementActionsRemote.ElementRequest(this, "input", element, timeout);
                req.Value = value;
                HtmlElementActionsRemote.Run(this, req, "input");
                return;
            }

            HtmlElementActions.Input(this, element, value, timeout);
        }

        public void Check(IELocator locator, int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            if (RemoteDomEnabled)
            {
                HtmlElementActionsRemote.Run(
                    this,
                    HtmlElementActionsRemote.LocatorRequest(this, "check", locator, timeout),
                    "check");
                return;
            }

            HtmlElementActions.Check(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
        }

        public void Check(IEHtmlElement element, int timeout = OperationDefaults.TimeoutMs)
        {
            if (RemoteDomEnabled)
            {
                HtmlElementActionsRemote.Run(
                    this,
                    HtmlElementActionsRemote.ElementRequest(this, "check", element, timeout),
                    "check");
                return;
            }

            HtmlElementActions.Check(this, element, timeout);
        }

        public void Uncheck(IELocator locator, int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            if (RemoteDomEnabled)
            {
                HtmlElementActionsRemote.Run(
                    this,
                    HtmlElementActionsRemote.LocatorRequest(this, "uncheck", locator, timeout),
                    "uncheck");
                return;
            }

            HtmlElementActions.Uncheck(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
        }

        public void Uncheck(IEHtmlElement element, int timeout = OperationDefaults.TimeoutMs)
        {
            if (RemoteDomEnabled)
            {
                HtmlElementActionsRemote.Run(
                    this,
                    HtmlElementActionsRemote.ElementRequest(this, "uncheck", element, timeout),
                    "uncheck");
                return;
            }

            HtmlElementActions.Uncheck(this, element, timeout);
        }

        public bool IsChecked(IELocator locator, int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            if (RemoteDomEnabled)
                return HtmlElementActionsRemote.RunBool(
                    this,
                    HtmlElementActionsRemote.LocatorRequest(this, "ischecked", locator, timeout),
                    "ischecked");

            return HtmlElementActions.IsChecked(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
        }

        public bool IsChecked(IEHtmlElement element, int timeout = OperationDefaults.TimeoutMs)
        {
            if (RemoteDomEnabled)
                return HtmlElementActionsRemote.RunBool(
                    this,
                    HtmlElementActionsRemote.ElementRequest(this, "ischecked", element, timeout),
                    "ischecked");

            return HtmlElementActions.IsChecked(this, element, timeout);
        }

        public void Select(IELocator locator, int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            if (RemoteDomEnabled)
            {
                HtmlElementActionsRemote.Run(
                    this,
                    HtmlElementActionsRemote.LocatorRequest(this, "select", locator, timeout),
                    "select");
                return;
            }

            HtmlElementActions.Select(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
        }

        public string GetText(IELocator locator, int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            if (RemoteDomEnabled)
                return HtmlElementActionsRemote.RunString(
                    this,
                    HtmlElementActionsRemote.LocatorRequest(this, "gettext", locator, timeout),
                    "gettext");

            return HtmlElementActions.GetText(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
        }

        public string GetText(IEHtmlElement element, int timeout = OperationDefaults.TimeoutMs)
        {
            if (RemoteDomEnabled)
                return HtmlElementActionsRemote.RunString(
                    this,
                    HtmlElementActionsRemote.ElementRequest(this, "gettext", element, timeout),
                    "gettext");

            return HtmlElementActions.GetText(this, element, timeout);
        }

        public string GetValue(IELocator locator, int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            if (RemoteDomEnabled)
                return HtmlElementActionsRemote.RunString(
                    this,
                    HtmlElementActionsRemote.LocatorRequest(this, "getvalue", locator, timeout),
                    "getvalue");

            return HtmlElementActions.GetValue(this, locator.ParseElement(), locator.ParseFramePath(), timeout);
        }

        public string GetValue(IEHtmlElement element, int timeout = OperationDefaults.TimeoutMs)
        {
            if (RemoteDomEnabled)
                return HtmlElementActionsRemote.RunString(
                    this,
                    HtmlElementActionsRemote.ElementRequest(this, "getvalue", element, timeout),
                    "getvalue");

            return HtmlElementActions.GetValue(this, element, timeout);
        }

        public string GetAttribute(
            IELocator locator,
            string attributeName,
            int timeout = OperationDefaults.TimeoutMs)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            if (RemoteDomEnabled)
            {
                var req = HtmlElementActionsRemote.LocatorRequest(this, "getattribute", locator, timeout);
                req.AttributeName = attributeName;
                return HtmlElementActionsRemote.RunString(this, req, "getattribute");
            }

            return HtmlElementActions.GetAttribute(
                this,
                locator.ParseElement(),
                attributeName,
                locator.ParseFramePath(),
                timeout);
        }

        public string GetAttribute(
            IEHtmlElement element,
            string attributeName,
            int timeout = OperationDefaults.TimeoutMs)
        {
            if (RemoteDomEnabled)
            {
                var req = HtmlElementActionsRemote.ElementRequest(this, "getattribute", element, timeout);
                req.AttributeName = attributeName;
                return HtmlElementActionsRemote.RunString(this, req, "getattribute");
            }

            return HtmlElementActions.GetAttribute(this, element, attributeName, timeout);
        }

        /// <summary>
        /// Run script in the frame from <paramref name="locator"/> (if any). Pass <paramref name="target"/> to bind DOM <c>element</c>.
        /// </summary>
        public IeScriptResult ExecuteScript(
            string script,
            IELocator locator = null,
            IEHtmlElement target = null,
            string argsJson = null)
        {
            if (RemoteDomEnabled)
            {
                var req = new Com.IeComHostDomRequest
                {
                    Op = "script",
                    Script = script,
                    FramePathJson = locator?.FramePath,
                    ArgsJson = argsJson,
                    Timeout = OperationDefaults.TimeoutMs
                };

                if (target?.IsRemote == true)
                {
                    req.TargetElementJson = Com.RemoteElementRefJson.WithIndex(
                        target.Remote.ElementJson,
                        target.Remote.ElementIdx);
                    if (!string.IsNullOrWhiteSpace(target.Remote.ScopeElementJson))
                    {
                        req.FramePathJson = null;
                        Com.HostDomRequestResolver.CopyScopeFromRemote(req, target.Remote);
                    }
                    else
                        req.FramePathJson = target.Remote.FramePathJson ?? locator?.FramePath;
                }

                var resp = Com.IeComHostDomBridge.Execute(Handle, req);
                Com.IeComHostDomBridge.EnsureOk(resp, "script");
                return new IeScriptResult(resp.Result);
            }

            return ExecuteScriptLocal(script, locator, target, argsJson);
        }

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

        /// <summary>Find via <paramref name="locator"/> then run script with that element as <c>element</c>.</summary>
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

        /// <summary>Wire page/demo script handlers on root document and nested go frame (if present).</summary>
        public void WireDemoHandlers()
        {
            if (RemoteDomEnabled)
            {
                ExecuteScript(
                    "if (typeof wireDemoHandlers === 'function') { wireDemoHandlers(); } if (typeof resetDemoCounters === 'function') { resetDemoCounters(); }",
                    null,
                    null,
                    null);
                return;
            }

            IeScriptHelper.EnsurePageHandlers(GetDocumentLocal(), "resetDemoCounters", "wireDemoHandlers");

            var goPath = new IELocator("{'id':'go'}", "[{'name':'f-actions'},{'name':'f-go'}]").ParseFramePath();
            if (goPath == null || goPath.Count == 0)
                return;

            var segments = FramePathParse.Parse(goPath);
            var goDoc = HtmlFrameHelper.TryGetFrameDocument(GetDocument(), segments);
            if (goDoc != null)
                IeScriptHelper.EnsurePageHandlers(goDoc, "wireDemoHandlers");
        }

        public override string ToString() =>
            $"EmbeddedIEWindow(Handle=0x{Handle.ToInt64():X}, Class={ClassName}, Title={Title})";
    }
}
