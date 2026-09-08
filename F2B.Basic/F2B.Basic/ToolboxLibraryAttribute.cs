using System;

namespace F2B.Basic
{
    /// <summary>
    /// Assembly-level toolbox root display name. Applied once in AssemblyInfo;
    /// activities only specify <see cref="ToolboxPathAttribute"/> relative folders.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class ToolboxLibraryAttribute : Attribute
    {
        public ToolboxLibraryAttribute(string displayName)
        {
            DisplayName = displayName ?? string.Empty;
        }

        /// <summary>Short name shown as the toolbox root (e.g. "Basic" instead of "F2B.Basic").</summary>
        public string DisplayName { get; }
    }
}