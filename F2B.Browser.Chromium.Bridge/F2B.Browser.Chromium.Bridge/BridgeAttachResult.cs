namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeAttachResult
    {
        public BridgeAttachResult(BwBrowser browser, BwTab tab)
        {
            Browser = browser;
            Tab = tab;
        }

        public BwBrowser Browser { get; }

        public BwTab Tab { get; }
    }
}
