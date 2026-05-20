using System;
using F2B.Browser.IExplore;
using F2B.Browser.IExplore.Com;

namespace IExplore.ComHost
{
    internal sealed class HostDomContext : ITridentDomHost
    {
        public HostDomContext(IntPtr handle, IntPtr ieServerHandle, string className)
        {
            Handle = handle;
            IeServerHandle = ieServerHandle;
            ClassName = className ?? string.Empty;
        }

        public IntPtr Handle { get; }
        public IntPtr IeServerHandle { get; }
        public string ClassName { get; }

        public IHTMLDocument2 GetMsHtmlDocument() => HtmlDocumentHelper.GetDocumentFromIeServer(IeServerHandle);

        public string Url
        {
            get
            {
                var shell = ShDocVwHelper.FindByHwnd((int)Handle.ToInt64());
                if (!string.IsNullOrEmpty(shell?.LocationUrl))
                    return shell.LocationUrl;
                return HtmlDocumentHelper.ReadDocumentUrl(GetMsHtmlDocument());
            }
        }

        public string Html => HtmlDocumentHelper.ReadOuterHtml(GetMsHtmlDocument());

        public void NavigateLocal(string url)
        {
            dynamic doc = GetMsHtmlDocument();
            doc.parentWindow.navigate(url);
        }

        public void Refresh(int timeout) => HtmlFrameHelper.Refresh(GetMsHtmlDocument(), timeout);

        public void WaitForFrame(IELocator locator, int timeout)
        {
            var path = locator.ParseFramePath();
            if (path == null || path.Count == 0)
                return;
            HtmlFrameHelper.WaitForFrameDocument(GetMsHtmlDocument(), path, timeout);
        }
    }
}
