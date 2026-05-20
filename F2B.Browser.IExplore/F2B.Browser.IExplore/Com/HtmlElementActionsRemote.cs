using System;
using F2B.Browser.IExplore;

namespace F2B.Browser.IExplore.Com
{
    internal static class HtmlElementActionsRemote
    {
        public static void Run(EmbeddedIEWindow window, IeComHostDomRequest request, string operationName)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            var response = IeComHostDomBridge.Execute(window.Handle, request);
            IeComHostDomBridge.EnsureOk(response, operationName);
        }

        public static string RunString(EmbeddedIEWindow window, IeComHostDomRequest request, string operationName)
        {
            var response = IeComHostDomBridge.Execute(window.Handle, request);
            IeComHostDomBridge.EnsureOk(response, operationName);
            return response.StringResult;
        }

        public static bool RunBool(EmbeddedIEWindow window, IeComHostDomRequest request, string operationName)
        {
            var response = IeComHostDomBridge.Execute(window.Handle, request);
            IeComHostDomBridge.EnsureOk(response, operationName);
            return response.BoolResult ?? false;
        }

        public static IeComHostDomRequest LocatorRequest(EmbeddedIEWindow window, string op, IELocator locator, int timeout)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            return new IeComHostDomRequest
            {
                Op = op,
                ElementJson = locator.Element,
                FramePathJson = locator.FramePath,
                Timeout = timeout
            };
        }

        public static IeComHostDomRequest ElementRequest(EmbeddedIEWindow window, string op, IEHtmlElement element, int timeout)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            if (!element.IsRemote)
                throw new ArgumentException("Element is not a remote ComHost reference.", nameof(element));

            var request = new IeComHostDomRequest
            {
                Op = op,
                ElementJson = RemoteElementRefJson.WithIndex(element.Remote.ElementJson, element.Remote.ElementIdx),
                FramePathJson = string.IsNullOrWhiteSpace(element.Remote.ScopeElementJson)
                    ? element.Remote.FramePathJson
                    : null,
                Timeout = timeout
            };
            HostDomRequestResolver.CopyScopeFromRemote(request, element.Remote);
            return request;
        }
    }
}
