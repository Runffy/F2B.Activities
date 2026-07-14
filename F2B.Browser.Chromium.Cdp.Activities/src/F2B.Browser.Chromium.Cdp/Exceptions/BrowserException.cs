using System;

namespace F2B.Browser.Chromium.Cdp.Exceptions
{
    public class BrowserException : Exception
    {
        public BrowserException(string message) : base(message)
        {
        }

        public BrowserException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
