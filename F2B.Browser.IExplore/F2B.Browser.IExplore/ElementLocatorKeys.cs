namespace F2B.Browser.IExplore
{
    /// <summary>Locator dictionary keys. HTML filters use short names; operation metadata uses <see cref="Prefix"/>.</summary>
    public static class ElementLocatorKeys
    {
        public const string Prefix = "F2B.Browser.IExplore.";

        /// <summary>HTML / element filters (also match DOM id, tagName, className, name).</summary>
        public const string Id = "id";
        public const string Tag = "tag";
        public const string Class = "class";
        public const string Name = "name";

        /// <summary>HTML <c>value</c> attribute filter (radio, input, etc.). Not Select option or Input text.</summary>
        public const string Value = "value";

        /// <summary>Text to type (<see cref="EmbeddedIEWindow.Input"/>).</summary>
        public const string InputText = Prefix + "value";

        /// <summary>Zero-based index when multiple elements match (<see cref="EmbeddedIEWindow.FindElement"/> / Click).</summary>
        public const string Idx = Prefix + "idx";

        /// <summary>Mouse button for Click / DoubleClick: left, middle, right.</summary>
        public const string ClickButton = Prefix + "click.button";

        /// <summary>Click mode: synthetic (default) or physical.</summary>
        public const string ClickMode = Prefix + "click.mode";

        /// <summary>Milliseconds between physical clicks (DoubleClick).</summary>
        public const string ClickInterval = Prefix + "click.interval";

        /// <summary>Select option label(s); comma-separated or array for multi-select.</summary>
        public const string SelectOptionText = Prefix + "select.text";

        /// <summary>Select option <c>value</c> attribute(s).</summary>
        public const string SelectOptionValue = Prefix + "select.value";

        /// <summary>Select option zero-based index(es).</summary>
        public const string SelectOptionIndex = Prefix + "select.index";
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
