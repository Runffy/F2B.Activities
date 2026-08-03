using System.Collections.Generic;
using F2B.Browser.Chromium.Cdp.Selectors;

namespace F2B.Browser.Chromium.Cdp.Inspector.Services
{
    /// <summary>
    /// Result of a CDP indicate pick session (placeholder for full indicate implementation).
    /// </summary>
    internal sealed class CdpIndicatePickResult
    {
        public bool Cancelled { get; set; }

        public string InvalidatedReason { get; set; }

        public IList<SelectorLevel> Levels { get; set; }

        public IList<SelectorLevel> MinimalLevels { get; set; }

        public string DisplayName { get; set; }
    }
}
