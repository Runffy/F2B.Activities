using System;

namespace F2B.Excel.CXML
{
    [Flags]
    public enum ExcelClearOptions
    {
        None = 0,
        Value = 1,
        Format = 2,
        Hyperlink = 4,
        Comment = 8,
        All = Value | Format | Hyperlink | Comment
    }
}
