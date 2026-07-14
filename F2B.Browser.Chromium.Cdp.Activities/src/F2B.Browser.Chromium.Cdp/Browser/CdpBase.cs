using System;

namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// Shared base for tab, frame, and element contexts.
    /// </summary>
    public abstract class CdpBase
    {
        /// <summary>Owning tab for this context.</summary>
        public abstract CdpTab Tab { get; }

        /// <summary>Executes JavaScript in this context.</summary>
        public abstract object RunJs(
            string script,
            object[] args = null,
            bool asExpression = false,
            bool isAsync = false,
            int timeoutMs = 30000);

        /// <summary>Finds the first element matching selector XML.</summary>
        public abstract CdpElement FindElement(string selectorXml, int timeoutMs = 15000, bool throwException = true);

        /// <summary>Finds all elements matching selector XML.</summary>
        public abstract CdpElement[] FindElements(string selectorXml);

        /// <summary>Returns whether an element matching selector XML exists.</summary>
        public abstract bool ElementExists(string selectorXml);

        /// <summary>Finds a frame matching selector XML (supports nested &lt;frm&gt;).</summary>
        public abstract CdpFrame FindFrame(string selectorXml, int timeoutMs = 15000, bool throwException = true);
    }
}
