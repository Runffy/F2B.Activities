namespace F2B.Browser.IExplore
{
    /// <summary>Reserved keys in element locator dictionaries (not HTML attributes).</summary>
    public static class ElementLocatorKeys
    {
        public const string Id = "id";
        public const string Tag = "tag";
        public const string Class = "class";
        public const string Name = "name";

        /// <summary>Zero-based index when multiple elements match (<see cref="EmbeddedIEWindow.FindElement"/> only; ignored by <see cref="EmbeddedIEWindow.FindElements"/>).</summary>
        public const string Idx = "idx";

        /// <summary>Text to type (<see cref="EmbeddedIEWindow.Input"/>).</summary>
        public const string Value = "value";

        /// <summary>Mouse button: left (default), middle, right (Click / DoubleClick).</summary>
        public const string Button = "button";

        /// <summary>Click mode: synthetic (default) or physical (Click / DoubleClick).</summary>
        public const string Mode = "mode";

        /// <summary>Milliseconds between physical clicks for DoubleClick (default 100).</summary>
        public const string Interval = "interval";

        public const string ClickMode = "clickmode";

        /// <summary>Option label (<see cref="EmbeddedIEWindow.Select"/> only).</summary>
        public const string OptionText = "text";

        /// <summary>Option value attribute (<see cref="EmbeddedIEWindow.Select"/> only).</summary>
        public const string OptionValue = "value";

        /// <summary>
        /// Option zero-based index (<see cref="EmbeddedIEWindow.Select"/> only).
        /// For element index use <see cref="Idx"/>.
        /// </summary>
        public const string OptionIndex = "index";
    }

    public enum MouseButton
    {
        Left,
        Middle,
        Right
    }

    /// <summary>
    /// <see cref="ClickMode.Synthetic"/> — DOM / script, no cursor movement.
    /// <see cref="ClickMode.Physical"/> — moves the system cursor and sends real mouse messages.
    /// </summary>
    public enum ClickMode
    {
        Synthetic,
        Physical
    }
}
