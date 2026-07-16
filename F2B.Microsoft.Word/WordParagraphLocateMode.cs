using System.ComponentModel;

namespace F2B.Microsoft.Word
{
    public enum WordParagraphLocateMode
    {
        [Description("Keyword")]
        Keyword = 0,

        [Description("Bookmark")]
        Bookmark = 1,

        [Description("Paragraph Index")]
        ParagraphIndex = 2
    }
}
