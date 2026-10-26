using System;

namespace F2B.Excel.CXML
{
    /// <summary>Relative toolbox folder under ToolboxLibrary.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ToolboxPathAttribute : Attribute
    {
        public ToolboxPathAttribute(string path)
        {
            Path = path ?? string.Empty;
        }

        public string Path { get; }
    }
}