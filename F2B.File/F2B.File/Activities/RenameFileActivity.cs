using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Rename File")]
    [Description("Rename a file.")]
    public sealed class RenameFileActivity : CodeActivity
    {
        public RenameFileActivity()
        {
            DisplayName = "Rename File";
        }

        [DisplayName("File Path")]
        [Description("Existing file path.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("New Name")]
        [Description("New file name or full destination path.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> NewName { get; set; }

        [DisplayName("Overwrite")]
        [Description("Overwrite the destination file if it already exists.")]
        [Category("Input.C")]
        [DefaultValue(false)]
        public InArgument<bool> Overwrite { get; set; } = false;

        [DisplayName("New Path")]
        [Description("Full path after rename.")]
        [Category("Output")]
        public OutArgument<string> NewPath { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var filePath = FileActivityHelper.RequirePath(FilePath, context, nameof(FilePath));
            var newName = FileActivityHelper.RequirePath(NewName, context, nameof(NewName));
            var overwrite = FileActivityHelper.GetOrDefault(Overwrite, context, false);

            if (!System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException("File was not found.", filePath);
            }

            var destinationPath = Path.IsPathRooted(newName)
                ? newName
                : Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, newName);

            if (System.IO.File.Exists(destinationPath) && !overwrite)
            {
                throw new IOException("Destination file already exists: " + destinationPath);
            }

            if (System.IO.File.Exists(destinationPath))
            {
                System.IO.File.Delete(destinationPath);
            }

            System.IO.File.Move(filePath, destinationPath);
            NewPath?.Set(context, destinationPath);
        }
    }
}
