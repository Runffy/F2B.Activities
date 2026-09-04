namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// One XAML file discovered under Libs.
    /// </summary>
    internal sealed class LibXamlEntry
    {
        public string FilePath { get; set; }
        public string DisplayName { get; set; }
        /// <summary>Toolbox category, e.g. Customized.MWS or Customized.Uncategorized.</summary>
        public string Category { get; set; }
        /// <summary>Safe CLR type name segment used for the emitted factory.</summary>
        public string TypeKey { get; set; }
    }
}
