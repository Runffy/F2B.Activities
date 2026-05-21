using System;

namespace F2B.Browser.IExplore
{
    /// <summary>Diagnostic snapshot of a Trident-capable top-level window (for troubleshooting Connect).</summary>
    public sealed class TridentWindowInfo
    {
        public IntPtr Handle { get; set; }
        public string ClassName { get; set; }
        public string WindowTitle { get; set; }
        public string DocumentTitle { get; set; }
        public string Url { get; set; }
        public bool DocumentAccessible { get; set; }

        public override string ToString()
        {
            return string.Format(
                "HWND=0x{0:X} Class={1} WinTitle=[{2}] DocTitle=[{3}] Url=[{4}] DocOk={5}",
                Handle.ToInt64(),
                ClassName ?? "",
                WindowTitle ?? "",
                DocumentTitle ?? "",
                Url ?? "",
                DocumentAccessible);
        }
    }
}
