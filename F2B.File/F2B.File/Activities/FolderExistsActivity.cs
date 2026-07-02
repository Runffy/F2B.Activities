using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Folder Exists")]
    [Description("Check whether a folder exists.")]
    public sealed class FolderExistsActivity : CodeActivity
    {
        public FolderExistsActivity()
        {
            DisplayName = "Folder Exists";
        }

        [DisplayName("Folder Path")]
        [Description("Path of the folder to check.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FolderPath { get; set; }

        [DisplayName("Result")]
        [Description("True when the folder exists.")]
        [Category("Output")]
        public OutArgument<bool> Result { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var folderPath = FileActivityHelper.RequirePath(FolderPath, context, nameof(FolderPath));
            Result?.Set(context, Directory.Exists(folderPath));
        }
    }
}
