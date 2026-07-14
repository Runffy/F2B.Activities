namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// HTTP response from in-tab synchronous XHR requests.
    /// </summary>
    public sealed class CdpResponse
    {
        internal CdpResponse(int statusCode, string text)
        {
            StatusCode = statusCode;
            Text = text ?? string.Empty;
        }

        public int StatusCode { get; private set; }

        public string Text { get; private set; }

        public override string ToString()
        {
            return string.Format("CdpResponse(StatusCode={0}, TextLength={1})", StatusCode, Text.Length);
        }
    }
}
