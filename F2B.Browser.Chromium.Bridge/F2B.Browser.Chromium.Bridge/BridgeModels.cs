using System.Collections.Generic;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BwTabInfo
    {
        public int TabId { get; set; }
        public string Url { get; set; }
        public string Title { get; set; }
        public bool IsClosed { get; set; }
        public bool Active { get; set; }
        public int Index { get; set; }
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
