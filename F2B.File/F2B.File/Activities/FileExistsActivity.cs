using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("File Exists")]
    [Description("Check whether a file exists.")]
    public sealed class FileExistsActivity : CodeActivity
    {
        public FileExistsActivity()
        {
            DisplayName = "File Exists";
        }

        [DisplayName("File Path")]
        [Description("Path of the file to check.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("Result")]
        [Description("True when the file exists.")]
        [Category("Output")]
        public OutArgument<bool> Result { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var filePath = FileActivityHelper.RequirePath(FilePath, context, nameof(FilePath));
            Result?.Set(context, System.IO.File.Exists(filePath));
        }
    }
}
