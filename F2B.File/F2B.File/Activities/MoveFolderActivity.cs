using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Move Folder")]
    [Description("Move a folder to a new location.")]
    public sealed class MoveFolderActivity : CodeActivity
    {
        public MoveFolderActivity()
        {
            DisplayName = "Move Folder";
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

        protected override void Execute(CodeActivityContext context)
        {
            var sourcePath = FileActivityHelper.RequirePath(SourcePath, context, nameof(SourcePath));
            var destinationPath = FileActivityHelper.RequirePath(DestinationPath, context, nameof(DestinationPath));

            if (!Directory.Exists(sourcePath))
            {
                throw new DirectoryNotFoundException("Source folder was not found: " + sourcePath);
            }

            FileActivityHelper.EnsureParentDirectoryExists(destinationPath);
            Directory.Move(sourcePath, destinationPath);
        }
    }
}
