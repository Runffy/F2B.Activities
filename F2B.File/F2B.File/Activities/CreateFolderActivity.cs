using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Create Folder")]
    [Description("Create a folder and any missing parent folders.")]
    public sealed class CreateFolderActivity : CodeActivity
    {
        public CreateFolderActivity()
        {
            DisplayName = "Create Folder";
        }

        [DisplayName("Folder Path")]
        [Description("Path of the folder to create.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FolderPath { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var folderPath = FileActivityHelper.RequirePath(FolderPath, context, nameof(FolderPath));
            Directory.CreateDirectory(folderPath);
        }
    }
}
