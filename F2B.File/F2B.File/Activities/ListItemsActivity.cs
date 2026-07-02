using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("List Items")]
    [Description("Returns full paths of files and subfolders in the target folder (first level only). Names are matched against an optional regular expression filter.")]
    public sealed class ListItemsActivity : CodeActivity
    {
        public ListItemsActivity()
        {
            DisplayName = "List Items";
        }

        [DisplayName("Folder Path")]
        [Description("Folder to list files and subfolders from.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FolderPath { get; set; }

        [DisplayName("Filter")]
        [Description("Optional regular expression matched against file and folder names. Empty means include all items.")]
        [Category("Input.B")]
        public InArgument<string> Filter { get; set; }

        [DisplayName("Items")]
        [Description("Full paths of matching files and subfolders in the folder.")]
        [Category("Output")]
        public OutArgument<string[]> Items { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var folderPath = FileActivityHelper.RequirePath(FolderPath, context, nameof(FolderPath));
            var filter = FileActivityHelper.GetOrDefault(Filter, context, string.Empty);

            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException("Folder was not found: " + folderPath);

            Items?.Set(context, FileActivityHelper.ListFilteredItems(folderPath, filter));
        }
    }
}
