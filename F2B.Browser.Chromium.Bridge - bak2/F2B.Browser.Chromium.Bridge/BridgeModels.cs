using System.Collections.Generic;

namespace F2B.Browser.Chromium.Bridge
{
    public class BwTabInfo
    {
        public int TabId { get; set; }
        public int WindowId { get; set; }
        public string Url { get; set; }
        public string Title { get; set; }
        public bool IsClosed { get; set; }
        public bool Active { get; set; }
        public int Index { get; set; }
        public BridgeTabLoadStatus LoadStatus { get; set; }
        public string LoadStatusText { get; set; }
        public bool IsErrorPage { get; set; }
        public bool IsRestrictedUrl { get; set; }
        public bool IsBlankPage { get; set; }

        public bool IsLoading => LoadStatus == BridgeTabLoadStatus.Loading;

        public bool IsLoadComplete => LoadStatus == BridgeTabLoadStatus.Complete;
    }

    /// <summary>
    /// Snapshot of the browser's currently activated tab and page state.
    /// </summary>
    public sealed class BwBrowserStatus : BwTabInfo
    {
        public string BrowserInstanceId { get; set; }

        public int BrowserWindowId { get; set; }

        public bool HasActivatedTab { get; set; }

        public BwTab ActivatedTab { get; set; }

        /// <summary>
        /// True when the page finished loading but is an error/restricted/blank page.
        /// Useful for diagnosing element-not-found failures caused by a broken navigation.
        /// </summary>
        public bool IsLikelyBrokenPage =>
            HasActivatedTab &&
            IsLoadComplete &&
            (IsErrorPage || IsBlankPage || IsRestrictedUrl);
    }

    public sealed class BwRect
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public sealed class BwCookie
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Domain { get; set; }
        public string Path { get; set; }
    }

    public sealed class BwDownloadInfo
    {
        public string Url { get; set; }
        public string SuggestedFileName { get; set; }
        public string SavedPath { get; set; }
    }
}
