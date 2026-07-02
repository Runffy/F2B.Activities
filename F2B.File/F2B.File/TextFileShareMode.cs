using System.ComponentModel;

namespace F2B.File
{
    public enum TextFileShareMode
    {
        [Description("No sharing; exclusive access.")]
        None,

        [Description("Allow other processes to read the file.")]
        Read,

        [Description("Allow other processes to write to the file.")]
        Write,

        [Description("Allow other processes to read and write the file.")]
        ReadWrite,

        [Description("Allow other processes to delete the file.")]
        Delete
    }
}
