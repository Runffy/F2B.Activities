using System.ComponentModel;

namespace F2B.Microsoft.Word
{
    public enum WordPageBreakLocateMode
    {
        [Description("Document End")]
        DocumentEnd = 0,

        [Description("Document Start")]
        DocumentStart = 1,

        [Description("Keyword")]
        Keyword = 2,

        [Description("Bookmark")]
        Bookmark = 3
    }
}
