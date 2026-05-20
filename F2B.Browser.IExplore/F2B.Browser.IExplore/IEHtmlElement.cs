using System;
using F2B.Browser.IExplore.Com;

namespace F2B.Browser.IExplore
{
    /// <summary>Handle to an MSHTML DOM element returned by <see cref="EmbeddedIEWindow.FindElement"/> / <see cref="EmbeddedIEWindow.FindElements"/>.</summary>
    public sealed class IEHtmlElement
    {
        internal IEHtmlElement(object comElement)
        {
            if (!ComElementHelper.IsValidElement(comElement))
                throw new ArgumentException("Invalid MSHTML element.", nameof(comElement));

            Raw = comElement;
        }

        internal IEHtmlElement(RemoteElementRef remote)
        {
            Remote = remote ?? throw new ArgumentNullException(nameof(remote));
        }

        /// <summary>Underlying COM object (for advanced interop). Null when <see cref="Remote"/> is set.</summary>
        public object Raw { get; }

        internal RemoteElementRef Remote { get; }

        public bool IsRemote => Remote != null;

        internal static IEHtmlElement From(object comElement)
        {
            if (comElement == null)
                return null;
            return new IEHtmlElement(comElement);
        }

        internal static IEHtmlElement FromRemote(
            long windowHandle,
            string elementJson,
            string framePathJson,
            int? elementIdx,
            IEHtmlElement scopeParent = null)
        {
            string scopeElementJson = null;
            string scopeFramePathJson = null;
            int scopeElementIdx = -1;

            if (scopeParent?.Remote != null)
            {
                var parent = scopeParent.Remote;
                scopeElementJson = RemoteElementRefJson.WithIndex(parent.ElementJson, parent.ElementIdx);
                scopeFramePathJson = parent.FramePathJson;
            }

            return new IEHtmlElement(new RemoteElementRef
            {
                WindowHandle = windowHandle,
                ElementJson = elementJson,
                FramePathJson = scopeParent != null ? null : framePathJson,
                ElementIdx = elementIdx,
                ScopeElementJson = scopeElementJson,
                ScopeFramePathJson = scopeFramePathJson,
                ScopeElementIdx = scopeElementIdx
            });
        }

        internal static object Unwrap(IEHtmlElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            if (element.Remote != null)
                throw new InvalidOperationException("Cannot unwrap a remote element; use locator-based ComHost operations.");
            return element.Raw;
        }

        internal sealed class RemoteElementRef
        {
            public long WindowHandle;
            public string ElementJson;
            public string FramePathJson;
            public int? ElementIdx;
            public string ScopeElementJson;
            public string ScopeFramePathJson;
            public int ScopeElementIdx = -1;
        }
    }
}
