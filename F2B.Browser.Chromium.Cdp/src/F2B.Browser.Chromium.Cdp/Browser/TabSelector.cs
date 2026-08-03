using System;
using System.Text.RegularExpressions;
using System.Xml;
using F2B.Browser.Chromium.Cdp.Exceptions;
using F2B.Browser.Chromium.Cdp.Internal;

namespace F2B.Browser.Chromium.Cdp.Browser
{
    internal sealed class TabSelector
    {
        private readonly Regex _titleRegex;
        private readonly Regex _urlRegex;

        public string Title { get; private set; }

        public string TitleRegexPattern { get; private set; }

        public string Url { get; private set; }

        public string UrlRegexPattern { get; private set; }

        public string Browser { get; private set; }

        public int? Port { get; private set; }

        /// <summary>
        /// Zero-based index among all matched tabs. Defaults to 0.
        /// </summary>
        public int Idx { get; private set; }

        public static TabSelector Parse(string selectorXml)
        {
            if (string.IsNullOrWhiteSpace(selectorXml))
            {
                throw new BrowserException("Selector XML cannot be empty.");
            }

            var document = new XmlDocument();
            try
            {
                document.LoadXml(selectorXml.Trim());
            }
            catch (Exception ex)
            {
                throw new BrowserException("Invalid selector XML.", ex);
            }

            var root = document.DocumentElement;
            if (root == null || !string.Equals(root.Name, "wnd", StringComparison.OrdinalIgnoreCase))
            {
                throw new BrowserException("Selector XML root element must be <wnd>.");
            }

            return new TabSelector(root);
        }

        private TabSelector(XmlElement root)
        {
            Title = GetOptionalAttribute(root, "title");
            TitleRegexPattern = GetOptionalAttribute(root, "title-re");
            Url = GetOptionalAttribute(root, "url");
            UrlRegexPattern = GetOptionalAttribute(root, "url-re");
            Browser = GetOptionalAttribute(root, "browser");

            var portValue = GetOptionalAttribute(root, "port");
            if (!string.IsNullOrWhiteSpace(portValue))
            {
                int port;
                if (!int.TryParse(portValue, out port) || port <= 0)
                {
                    throw new BrowserException(string.Format("Invalid port in selector XML: {0}", portValue));
                }

                Port = port;
            }

            var idxValue = GetOptionalAttribute(root, "idx");
            if (idxValue != null)
            {
                int idx;
                if (!int.TryParse(idxValue, out idx) || idx < 0)
                {
                    throw new BrowserException(string.Format("Invalid idx in selector XML: {0}", idxValue));
                }

                Idx = idx;
            }
            else
            {
                Idx = 0;
            }

            if (Title == null && TitleRegexPattern == null && Url == null && UrlRegexPattern == null
                && Browser == null && !Port.HasValue && idxValue == null)
            {
                throw new BrowserException("Selector XML must contain at least one matching attribute.");
            }

            if (TitleRegexPattern != null)
            {
                _titleRegex = new Regex(TitleRegexPattern);
            }

            if (UrlRegexPattern != null)
            {
                _urlRegex = new Regex(UrlRegexPattern);
            }
        }

        public bool MatchesBrowser(string browserName)
        {
            return BrowserNameHelper.MatchesBrowserFilter(browserName, Browser);
        }

        public bool MatchesTab(CdpTargetInfo target)
        {
            if (target == null)
            {
                return false;
            }

            if (Title != null && !string.Equals(target.Title ?? string.Empty, Title, StringComparison.Ordinal))
            {
                return false;
            }

            if (_titleRegex != null && !_titleRegex.IsMatch(target.Title ?? string.Empty))
            {
                return false;
            }

            if (Url != null && !string.Equals(target.Url ?? string.Empty, Url, StringComparison.Ordinal))
            {
                return false;
            }

            if (_urlRegex != null && !_urlRegex.IsMatch(target.Url ?? string.Empty))
            {
                return false;
            }

            return true;
        }

        private static string GetOptionalAttribute(XmlElement element, string attributeName)
        {
            var value = element.GetAttribute(attributeName);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
