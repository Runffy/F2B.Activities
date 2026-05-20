namespace F2B.Browser.IExplore
{
    /// <summary>Dictionary keys for <see cref="EmbeddedIExplore.Connect"/>.</summary>
    public static class IEConnectCriteria
    {
        /// <summary>Window title or document.title must contain this substring (case-insensitive).</summary>
        public const string Title = "title";

        /// <summary>Window title or document.title must match this .NET regex.</summary>
        public const string TitleRegex = "title_regex";

        /// <summary>Document URL must contain this substring (case-insensitive).</summary>
        public const string Url = "url";

        /// <summary>Document URL must match this .NET regex.</summary>
        public const string UrlRegex = "url_regex";

        /// <summary>Top-level window class name must equal this value (case-insensitive).</summary>
        public const string ClassName = "classname";
    }
}
