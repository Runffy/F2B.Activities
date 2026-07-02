using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("List Folders")]
    [Description("Returns full paths of subfolders in the target folder (first level only). Names are matched against an optional regular expression filter.")]
    public sealed class ListFoldersActivity : CodeActivity
    {
        public ListFoldersActivity()
        {
            DisplayName = "List Folders";
        }

        [DisplayName("Folder Path")]
        [Description("Folder to list subfolders from.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FolderPath { get; set; }

        [DisplayName("Filter")]
        [Description("Optional regular expression matched against folder names. Empty means include all subfolders.")]
        [Category("Input.B")]
        public InArgument<string> Filter { get; set; }

        [DisplayName("Folders")]
        [Description("Full paths of matching subfolders in the folder.")]
        [Category("Output")]
        public OutArgument<string[]> Folders { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var folderPath = FileActivityHelper.RequirePath(FolderPath, context, nameof(FolderPath));
            var filter = FileActivityHelper.GetOrDefault(Filter, context, string.Empty);

            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException("Folder was not found: " + folderPath);

            Folders?.Set(context, FileActivityHelper.ListFilteredFolders(folderPath, filter));
        }
    }
}
