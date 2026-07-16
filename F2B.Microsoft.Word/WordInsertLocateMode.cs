using System.ComponentModel;

namespace F2B.Microsoft.Word
{
    public enum WordInsertLocateMode
    {
        [Description("Bookmark")]
        Bookmark = 0,

        [Description("Keyword")]
        Keyword = 1,

        [Description("Document Start")]
        DocumentStart = 2
    }
}
