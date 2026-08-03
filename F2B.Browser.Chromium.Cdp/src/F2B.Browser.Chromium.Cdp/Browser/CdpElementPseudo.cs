namespace F2B.Browser.Chromium.Cdp.Browser
{
    public sealed class CdpElementPseudo
    {
        private readonly CdpElement _element;
        private readonly Internal.CdpElementContext _context;

        internal CdpElementPseudo(CdpElement element, Internal.CdpElementContext context)
        {
            _element = element;
            _context = context;
        }

        public string Before
        {
            get { return ReadPseudoContent("::before", ":before", "before"); }
        }

        public string After
        {
            get { return ReadPseudoContent("::after", ":after", "after"); }
        }

        private string ReadPseudoContent(params string[] pseudoNames)
        {
            foreach (var pseudo in pseudoNames)
            {
                var content = _element.Style("content", pseudo);
                if (!string.IsNullOrEmpty(content) && !string.Equals(content, "content", System.StringComparison.Ordinal))
                {
                    return content;
                }
            }

            return null;
        }
    }
}
