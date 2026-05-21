namespace F2B.Browser.IExplore
{
    /// <summary>Keys for each segment in a nested frame path (<see cref="EmbeddedIEWindow.WaitForFrame"/>).</summary>
    public static class FrameLocatorKeys
    {
        /// <summary>Frame <c>name</c> attribute / <c>window.frames[name]</c>.</summary>
        public const string Name = "name";

        /// <summary>iframe / frame element <c>id</c> in the parent document.</summary>
        public const string Id = "id";

        /// <summary>Zero-based index in <c>window.frames</c>.</summary>
        public const string Index = "index";

        /// <summary>Substring matched against frame document URL or iframe <c>src</c>.</summary>
        public const string Src = "src";
    }
}
