using System;

namespace F2B.Excel.CXML
{
    /// <summary>Assembly-level toolbox root display name.</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class ToolboxLibraryAttribute : Attribute
    {
        public ToolboxLibraryAttribute(string displayName)
        {
            DisplayName = displayName ?? string.Empty;
        }

        public string DisplayName { get; }
    }
}