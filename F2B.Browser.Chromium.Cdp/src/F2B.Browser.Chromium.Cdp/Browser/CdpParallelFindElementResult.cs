namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// Result of <see cref="CdpTab.ParallelFindElement"/>.
    /// </summary>
    public sealed class CdpParallelFindElementResult
    {
        internal CdpParallelFindElementResult(int index, CdpElement element)
        {
            Index = index;
            Element = element;
        }

        /// <summary>Zero-based index in the selector list, or -1 when nothing matched.</summary>
        public int Index { get; private set; }

        /// <summary>The first matched element, or null when nothing matched.</summary>
        public CdpElement Element { get; private set; }

        /// <summary>Whether a selector matched within the timeout.</summary>
        public bool Found
        {
            get { return Index >= 0 && Element != null; }
        }

        public static CdpParallelFindElementResult NotFound()
        {
            return new CdpParallelFindElementResult(-1, null);
        }

        /// <summary>Creates a parallel find result for activity callers.</summary>
        public static CdpParallelFindElementResult Create(int index, CdpElement element)
        {
            return new CdpParallelFindElementResult(index, element);
        }
    }
}
