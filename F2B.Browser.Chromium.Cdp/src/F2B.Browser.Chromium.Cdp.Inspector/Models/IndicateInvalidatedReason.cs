namespace F2B.Browser.Chromium.Cdp.Inspector.Models
{
    internal static class IndicateInvalidatedReason
    {
        public const string TabChanged = "tab-changed";
        public const string PageNavigated = "page-navigated";
        public const string TabClosed = "tab-closed";
        public const string RestrictedTab = "restricted-tab";
    }
}
