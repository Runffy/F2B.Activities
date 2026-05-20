using System;
using System.Collections.Generic;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>Resolves scoped DOM targets for ComHost (x86) from <see cref="IeComHostDomRequest"/>.</summary>
    internal static class HostDomRequestResolver
    {
        public static bool HasScope(IeComHostDomRequest request) =>
            request != null && !string.IsNullOrWhiteSpace(request.ScopeElementJson);

        public static IEHtmlElement ResolveScope(ITridentDomHost window, IeComHostDomRequest request, int timeout)
        {
            if (!HasScope(request))
                return null;

            var scopeDict = IEJsonParse.ParseDictionary(request.ScopeElementJson);
            if (request.ScopeElementIdx >= 0)
                scopeDict[ElementLocatorKeys.Idx] = request.ScopeElementIdx.ToString();

            return HtmlElementActions.FindElement(
                window,
                scopeDict,
                IEJsonParse.ParseFramePath(request.ScopeFramePathJson),
                null,
                timeout);
        }

        public static IEHtmlElement FindElement(ITridentDomHost window, IeComHostDomRequest request, int timeout) =>
            FindElement(window, request, timeout, stripInputTextValue: false);

        public static IEHtmlElement FindElement(
            ITridentDomHost window,
            IeComHostDomRequest request,
            int timeout,
            bool stripInputTextValue)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var scope = ResolveScope(window, request, timeout);
            var element = IEJsonParse.ParseDictionary(request.ElementJson);
            if (stripInputTextValue)
                IEJsonParse.RemoveKeyIgnoreCase(element, ElementLocatorKeys.Value);

            return HtmlElementActions.FindElement(
                window,
                element,
                scope == null ? IEJsonParse.ParseFramePath(request.FramePathJson) : null,
                scope,
                timeout);
        }

        public static IEHtmlElement[] FindElements(ITridentDomHost window, IeComHostDomRequest request, int timeout)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var scope = ResolveScope(window, request, timeout);
            return HtmlElementActions.FindElements(
                window,
                IEJsonParse.ParseDictionary(request.ElementJson),
                scope == null ? IEJsonParse.ParseFramePath(request.FramePathJson) : null,
                scope,
                timeout);
        }

        public static bool ElementExists(ITridentDomHost window, IeComHostDomRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var scope = HasScope(request)
                ? ResolveScope(window, request, OperationDefaults.TimeoutMs)
                : null;

            return HtmlElementActions.ElementExists(
                window,
                IEJsonParse.ParseDictionary(request.ElementJson),
                scope == null ? IEJsonParse.ParseFramePath(request.FramePathJson) : null,
                scope);
        }

        public static void CopyScopeFromRemote(IeComHostDomRequest request, IEHtmlElement.RemoteElementRef remote)
        {
            if (request == null || remote == null || string.IsNullOrWhiteSpace(remote.ScopeElementJson))
                return;

            request.ScopeElementJson = remote.ScopeElementJson;
            request.ScopeFramePathJson = remote.ScopeFramePathJson;
            request.ScopeElementIdx = remote.ScopeElementIdx;
        }

        public static void CopyScopeFromParent(IeComHostDomRequest request, IEHtmlElement scopeParent)
        {
            if (request == null || scopeParent == null || !scopeParent.IsRemote)
                return;

            var parent = scopeParent.Remote;
            request.ScopeElementJson = RemoteElementRefJson.WithIndex(parent.ElementJson, parent.ElementIdx);
            request.ScopeFramePathJson = parent.FramePathJson;
            request.ScopeElementIdx = -1;
        }
    }
}
