using System;
using System.Runtime.InteropServices;

namespace F2B.Browser.IExplore.Com
{
    [ComImport]
    [Guid("332C4425-26CB-11D0-B483-00C04FD90119")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IHTMLDocument2
    {
        // Do not add url / readyState here — typed DISP getters can AV on half-ready docs.
        // Use HtmlDocumentHelper.ReadDocumentUrl / ReadDocumentTitle (dynamic + ComSafe).
        [DispId(1003)] object all { get; }
        [DispId(1005)] object body { get; }
        [DispId(1067)] IHTMLWindow2 parentWindow { get; }
        [DispId(1079)] object getElementsByTagName(string tagName);
        [DispId(1080)] object getElementById(string elementId);
        [DispId(1081)] object getElementsByName(string elementName);
    }

    [ComImport]
    [Guid("3050F4B2-98B5-11CF-BB82-00AA00BDCE0B")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IHTMLWindow2
    {
        [DispId(1001)] IHTMLDocument2 document { get; }
    }

    public static class MsHtmlGuids
    {
        public static readonly Guid IID_IHTMLDocument2 = new Guid("332C4425-26CB-11D0-B483-00C04FD90119");
    }
}
