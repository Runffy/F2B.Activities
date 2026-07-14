using System;

namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// Page and window geometry for a tab.
    /// </summary>
    public sealed class CdpTabRect
    {
        private readonly CdpTab _tab;

        internal CdpTabRect(CdpTab tab)
        {
            _tab = tab;
        }

        /// <summary>Page content size (width, height).</summary>
        public Tuple<int, int> Size
        {
            get { return _tab.SessionQueryRect().Size; }
        }

        /// <summary>Browser window size (width, height).</summary>
        public Tuple<int, int> WindowSize
        {
            get { return _tab.SessionQueryRect().WindowSize; }
        }

        /// <summary>Browser window location on screen (x, y).</summary>
        public Tuple<int, int> WindowLocation
        {
            get { return _tab.SessionQueryRect().WindowLocation; }
        }

        /// <summary>Window state: normal, fullscreen, maximized, or minimized.</summary>
        public string WindowState
        {
            get { return _tab.SessionQueryRect().WindowState; }
        }

        /// <summary>Viewport size without scrollbar (width, height).</summary>
        public Tuple<int, int> ViewportSize
        {
            get { return _tab.SessionQueryRect().ViewportSize; }
        }

        /// <summary>Viewport size including scrollbar (width, height).</summary>
        public Tuple<int, int> ViewportSizeWithScrollbar
        {
            get { return _tab.SessionQueryRect().ViewportSizeWithScrollbar; }
        }

        /// <summary>Page top-left location on screen (x, y).</summary>
        public Tuple<int, int> PageLocation
        {
            get { return _tab.SessionQueryRect().PageLocation; }
        }

        /// <summary>Viewport top-left location on screen (x, y).</summary>
        public Tuple<int, int> ViewportLocation
        {
            get { return _tab.SessionQueryRect().ViewportLocation; }
        }

        /// <summary>Page scroll position (x, y).</summary>
        public Tuple<int, int> ScrollPosition
        {
            get { return _tab.SessionQueryRect().ScrollPosition; }
        }
    }

    internal sealed class CdpTabRectSnapshot
    {
        public Tuple<int, int> Size { get; set; }

        public Tuple<int, int> WindowSize { get; set; }

        public Tuple<int, int> WindowLocation { get; set; }

        public string WindowState { get; set; }

        public Tuple<int, int> ViewportSize { get; set; }

        public Tuple<int, int> ViewportSizeWithScrollbar { get; set; }

        public Tuple<int, int> PageLocation { get; set; }

        public Tuple<int, int> ViewportLocation { get; set; }

        public Tuple<int, int> ScrollPosition { get; set; }
    }
}
