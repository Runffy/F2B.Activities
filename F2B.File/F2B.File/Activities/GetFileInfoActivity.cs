using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Get File Info")]
    [Description("Read basic file metadata such as size, dates, name, and extension.")]
    public sealed class GetFileInfoActivity : CodeActivity
    {
        public GetFileInfoActivity()
        {
            DisplayName = "Get File Info";
        }

        [DisplayName("File Path")]
        [Description("Path of the file.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("Name")]
        [Description("File name without directory.")]
        [Category("Output")]
        public OutArgument<string> Name { get; set; }

        [DisplayName("Extension")]
        [Description("File extension including the dot.")]
        [Category("Output")]
        public OutArgument<string> Extension { get; set; }

        [DisplayName("Size")]
        [Description("File size in bytes.")]
        [Category("Output")]
        public OutArgument<long> Size { get; set; }

        [DisplayName("Creation Time")]
        [Description("File creation time.")]
        [Category("Output")]
        public OutArgument<DateTime> CreationTime { get; set; }

        [DisplayName("Last Write Time")]
        [Description("Last write time.")]
        [Category("Output")]
        public OutArgument<DateTime> LastWriteTime { get; set; }

        [DisplayName("Last Access Time")]
        [Description("Last access time.")]
        [Category("Output")]
        public OutArgument<DateTime> LastAccessTime { get; set; }

        [DisplayName("Full Path")]
        [Description("Full file path.")]
        [Category("Output")]
        public OutArgument<string> FullPath { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var filePath = FileActivityHelper.RequirePath(FilePath, context, nameof(FilePath));

            if (!System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException("File was not found.", filePath);
            }

            var info = new FileInfo(filePath);

            Name?.Set(context, info.Name);
            Extension?.Set(context, info.Extension);
            Size?.Set(context, info.Length);
            CreationTime?.Set(context, info.CreationTime);
            LastWriteTime?.Set(context, info.LastWriteTime);
            LastAccessTime?.Set(context, info.LastAccessTime);
            FullPath?.Set(context, info.FullName);
        }
    }
}
