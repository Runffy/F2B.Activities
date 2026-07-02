using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Rename Folder")]
    [Description("Rename a folder.")]
    public sealed class RenameFolderActivity : CodeActivity
    {
        public RenameFolderActivity()
        {
            DisplayName = "Rename Folder";
        }

        [DisplayName("Folder Path")]
        [Description("Existing folder path.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FolderPath { get; set; }

        [DisplayName("New Name")]
        [Description("New folder name or full destination path.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> NewName { get; set; }

        [DisplayName("New Path")]
        [Description("Full path after rename.")]
        [Category("Output")]
        public OutArgument<string> NewPath { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var folderPath = FileActivityHelper.RequirePath(FolderPath, context, nameof(FolderPath));
            var newName = FileActivityHelper.RequirePath(NewName, context, nameof(NewName));

            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException("Folder was not found: " + folderPath);
            }

            var destinationPath = Path.IsPathRooted(newName)
                ? newName
                : Path.Combine(Path.GetDirectoryName(folderPath) ?? string.Empty, newName);

            if (Directory.Exists(destinationPath))
            {
                throw new IOException("Destination folder already exists: " + destinationPath);
            }

            Directory.Move(folderPath, destinationPath);
            NewPath?.Set(context, destinationPath);
        }
    }
}
