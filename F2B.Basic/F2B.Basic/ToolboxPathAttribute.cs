using System;

namespace F2B.Basic
{
    /// <summary>
    /// Relative toolbox folder under the assembly's <see cref="ToolboxLibraryAttribute"/> display name.
    /// Example: [ToolboxPath("Flow Control")] → Basic/Flow Control/{DisplayName}.
    /// Supports nested segments: [ToolboxPath("A/B/C")].
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ToolboxPathAttribute : Attribute
    {
        public ToolboxPathAttribute(string path)
        {
            Path = path ?? string.Empty;
        }

        /// <summary>Relative path segments separated by '/', without leading library name.</summary>
        public string Path { get; }
    }
}
