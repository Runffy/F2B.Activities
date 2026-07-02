using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Copy Folder")]
    [Description("Recursively copy a folder and its contents.")]
    public sealed class CopyFolderActivity : CodeActivity
    {
        public CopyFolderActivity()
        {
            DisplayName = "Copy Folder";
        }

        [DisplayName("Source Path")]
        [Description("Source folder path.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> SourcePath { get; set; }

        [DisplayName("Destination Path")]
        [Description("Destination folder path.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> DestinationPath { get; set; }

        [DisplayName("Overwrite")]
        [Description("Overwrite existing files in the destination folder.")]
        [Category("Input.C")]
        [DefaultValue(true)]
        public InArgument<bool> Overwrite { get; set; } = true;

        protected override void Execute(CodeActivityContext context)
        {
            var sourcePath = FileActivityHelper.RequirePath(SourcePath, context, nameof(SourcePath));
            var destinationPath = FileActivityHelper.RequirePath(DestinationPath, context, nameof(DestinationPath));
            var overwrite = FileActivityHelper.GetOrDefault(Overwrite, context, true);

            if (!Directory.Exists(sourcePath))
            {
                throw new DirectoryNotFoundException("Source folder was not found: " + sourcePath);
            }

            FileActivityHelper.EnsureParentDirectoryExists(destinationPath);
            FileActivityHelper.CopyDirectoryRecursive(sourcePath, destinationPath, overwrite);
        }
    }
}
