namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// Result of a successful FindTab operation.
    /// </summary>
    public sealed class CdpTabFindResult
    {
        internal CdpTabFindResult(CdpBrowser browser, CdpTab tab)
        {
            Browser = browser;
            Tab = tab;
        }

        public CdpBrowser Browser { get; private set; }

        public CdpTab Tab { get; private set; }
    }
}
