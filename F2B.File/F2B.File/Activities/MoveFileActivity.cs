using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Move File")]
    [Description("Move a file to a new location.")]
    public sealed class MoveFileActivity : CodeActivity
    {
        public MoveFileActivity()
        {
            DisplayName = "Move File";
        }

        [DisplayName("Source Path")]
        [Description("Source file path.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> SourcePath { get; set; }

        [DisplayName("Destination Path")]
        [Description("Destination file path.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> DestinationPath { get; set; }

        [DisplayName("Overwrite")]
        [Description("Overwrite the destination file if it already exists.")]
        [Category("Input.C")]
        [DefaultValue(false)]
        public InArgument<bool> Overwrite { get; set; } = false;

        protected override void Execute(CodeActivityContext context)
        {
            var sourcePath = FileActivityHelper.RequirePath(SourcePath, context, nameof(SourcePath));
            var destinationPath = FileActivityHelper.RequirePath(DestinationPath, context, nameof(DestinationPath));
            var overwrite = FileActivityHelper.GetOrDefault(Overwrite, context, false);

            FileActivityHelper.EnsureParentDirectoryExists(destinationPath);

            if (overwrite && System.IO.File.Exists(destinationPath))
            {
                System.IO.File.Delete(destinationPath);
            }

            System.IO.File.Move(sourcePath, destinationPath);
        }
    }
}
