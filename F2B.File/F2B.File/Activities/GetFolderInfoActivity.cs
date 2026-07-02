using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Get Folder Info")]
    [Description("Read basic folder metadata such as file count, folder count, and dates.")]
    public sealed class GetFolderInfoActivity : CodeActivity
    {
        public GetFolderInfoActivity()
        {
            DisplayName = "Get Folder Info";
        }

        [DisplayName("Folder Path")]
        [Description("Path of the folder.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FolderPath { get; set; }

        [DisplayName("Include Subdirectories")]
        [Description("Include nested files and folders in counts.")]
        [Category("Input.B")]
        [DefaultValue(false)]
        public InArgument<bool> IncludeSubdirectories { get; set; } = false;

        [DisplayName("Name")]
        [Description("Folder name without parent path.")]
        [Category("Output")]
        public OutArgument<string> Name { get; set; }

        [DisplayName("Full Path")]
        [Description("Full folder path.")]
        [Category("Output")]
        public OutArgument<string> FullPath { get; set; }

        [DisplayName("File Count")]
        [Description("Number of files in the folder.")]
        [Category("Output")]
        public OutArgument<int> FileCount { get; set; }

        [DisplayName("Folder Count")]
        [Description("Number of subfolders in the folder.")]
        [Category("Output")]
        public OutArgument<int> FolderCount { get; set; }

        [DisplayName("Total Size")]
        [Description("Total size in bytes of all files.")]
        [Category("Output")]
        public OutArgument<long> TotalSize { get; set; }

        [DisplayName("Creation Time")]
        [Description("Folder creation time.")]
        [Category("Output")]
        public OutArgument<DateTime> CreationTime { get; set; }

        [DisplayName("Last Write Time")]
        [Description("Last write time.")]
        [Category("Output")]
        public OutArgument<DateTime> LastWriteTime { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var folderPath = FileActivityHelper.RequirePath(FolderPath, context, nameof(FolderPath));
            var includeSubdirectories = FileActivityHelper.GetOrDefault(IncludeSubdirectories, context, false);

            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException("Folder was not found: " + folderPath);
            }

            var info = new DirectoryInfo(folderPath);
            var searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            var fileCount = Directory.GetFiles(folderPath, "*", searchOption).Length;
            var folderCount = Directory.GetDirectories(folderPath, "*", searchOption).Length;
            var totalSize = includeSubdirectories
                ? FileActivityHelper.GetDirectorySize(folderPath)
                : Directory.GetFiles(folderPath).Sum(file => new FileInfo(file).Length);

            Name?.Set(context, info.Name);
            FullPath?.Set(context, info.FullName);
            FileCount?.Set(context, fileCount);
            FolderCount?.Set(context, folderCount);
            TotalSize?.Set(context, totalSize);
            CreationTime?.Set(context, info.CreationTime);
            LastWriteTime?.Set(context, info.LastWriteTime);
        }
    }
}
