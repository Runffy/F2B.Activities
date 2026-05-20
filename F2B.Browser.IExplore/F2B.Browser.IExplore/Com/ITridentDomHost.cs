using System;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>MSHTML host surface used by <see cref="HtmlElementActions"/> (in-proc x86 only).</summary>
    internal interface ITridentDomHost
    {
        IntPtr Handle { get; }
        IntPtr IeServerHandle { get; }
        IHTMLDocument2 GetMsHtmlDocument();
    }
}
