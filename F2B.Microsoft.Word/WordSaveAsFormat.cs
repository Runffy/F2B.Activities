using System.ComponentModel;

namespace F2B.Microsoft.Word
{
    public enum WordSaveAsFormat
    {
        [Description("docx")]
        Docx = 0,

        [Description("doc")]
        Doc = 1,

        [Description("pdf")]
        Pdf = 2
    }
}
