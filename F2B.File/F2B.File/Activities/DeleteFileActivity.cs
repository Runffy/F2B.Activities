using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Delete File")]
    [Description("Delete a file if it exists.")]
    public sealed class DeleteFileActivity : CodeActivity
    {
        public DeleteFileActivity()
        {
            DisplayName = "Delete File";
        }

        [DisplayName("File Path")]
        [Description("Path of the file to delete.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("Deleted")]
        [Description("True when the file was deleted.")]
        [Category("Output")]
        public OutArgument<bool> Deleted { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var filePath = FileActivityHelper.RequirePath(FilePath, context, nameof(FilePath));
            var deleted = false;

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                deleted = true;
            }

            Deleted?.Set(context, deleted);
        }
    }
}
