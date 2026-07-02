using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("List Files")]
    [Description("Returns full paths of files in the target folder (first level only). Names are matched against an optional regular expression filter.")]
    public sealed class ListFilesActivity : CodeActivity
    {
        public ListFilesActivity()
        {
            DisplayName = "List Files";
        }

        [DisplayName("Folder Path")]
        [Description("Folder to list files from.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FolderPath { get; set; }

        [DisplayName("Filter")]
        [Description("Optional regular expression matched against file names. Empty means include all files.")]
        [Category("Input.B")]
        public InArgument<string> Filter { get; set; }

        [DisplayName("Files")]
        [Description("Full paths of matching files in the folder.")]
        [Category("Output")]
        public OutArgument<string[]> Files { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var folderPath = FileActivityHelper.RequirePath(FolderPath, context, nameof(FolderPath));
            var filter = FileActivityHelper.GetOrDefault(Filter, context, string.Empty);

            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException("Folder was not found: " + folderPath);

            Files?.Set(context, FileActivityHelper.ListFilteredFiles(folderPath, filter));
        }
    }
}
