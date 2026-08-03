namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// Result of a click-to-download operation.
    /// </summary>
    public sealed class CdpDownloadResult
    {
        internal CdpDownloadResult(string path, string suggestedFileName)
        {
            Path = path ?? string.Empty;
            SuggestedFileName = suggestedFileName ?? string.Empty;
        }

        public string Path { get; private set; }

        public string SuggestedFileName { get; private set; }
    }
}
