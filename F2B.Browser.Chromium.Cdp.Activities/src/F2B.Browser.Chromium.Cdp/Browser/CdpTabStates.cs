namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// Page loading and availability states for a tab.
    /// </summary>
    public sealed class CdpTabStates
    {
        private readonly CdpTab _tab;

        internal CdpTabStates(CdpTab tab)
        {
            _tab = tab;
        }

        /// <summary>
        /// Whether the page is currently loading.
        /// </summary>
        public bool IsLoading
        {
            get { return _tab.SessionQueryStates().IsLoading; }
        }

        /// <summary>
        /// Whether the tab is still available (not closed).
        /// </summary>
        public bool IsAlive
        {
            get { return _tab.SessionQueryStates().IsAlive; }
        }

        /// <summary>
        /// Page ready state: connecting, loading, interactive, or complete.
        /// </summary>
        public string ReadyState
        {
            get { return _tab.SessionQueryStates().ReadyState; }
        }

        /// <summary>
        /// Whether a JavaScript dialog (alert/confirm/prompt) is open.
        /// </summary>
        public bool HasAlert
        {
            get { return _tab.SessionQueryStates().HasAlert; }
        }
    }

    internal sealed class CdpTabStateSnapshot
    {
        public bool IsLoading { get; set; }

        public bool IsAlive { get; set; }

        public string ReadyState { get; set; }

        public bool HasAlert { get; set; }
    }
}
