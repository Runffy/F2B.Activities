using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Delete Folder")]
    [Description("Recursively delete a folder and its contents if it exists.")]
    public sealed class DeleteFolderActivity : CodeActivity
    {
        public DeleteFolderActivity()
        {
            DisplayName = "Delete Folder";
        }

        [DisplayName("Folder Path")]
        [Description("Path of the folder to delete.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FolderPath { get; set; }

        [DisplayName("Deleted")]
        [Description("True when the folder was deleted.")]
        [Category("Output")]
        public OutArgument<bool> Deleted { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var folderPath = FileActivityHelper.RequirePath(FolderPath, context, nameof(FolderPath));
            var deleted = false;

            if (Directory.Exists(folderPath))
            {
                FileActivityHelper.DeleteDirectoryRecursive(folderPath);
                deleted = true;
            }

            Deleted?.Set(context, deleted);
        }
    }
}
