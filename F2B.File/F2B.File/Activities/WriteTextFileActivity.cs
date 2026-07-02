using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Write Text File")]
    [Description("Write or overwrite text content in a file.")]
    public sealed class WriteTextFileActivity : CodeActivity
    {
        public WriteTextFileActivity()
        {
            DisplayName = "Write Text File";
        }

        [DisplayName("File Path")]
        [Description("Path of the text file to write.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("Content")]
        [Description("Text content to write.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> Content { get; set; }

        [DisplayName("Encoding")]
        [Description("Text encoding name, for example UTF-8 or UTF-8 BOM.")]
        [Category("Input.C")]
        [DefaultValue("UTF-8")]
        public InArgument<string> Encoding { get; set; } = "UTF-8";

        [DisplayName("Append")]
        [Description("Append content instead of overwriting the file.")]
        [Category("Input.D")]
        [DefaultValue(false)]
        public InArgument<bool> Append { get; set; } = false;

        [DisplayName("Share Mode")]
        [Description("File sharing mode while the file is open for writing.")]
        [Category("Input.E")]
        [DefaultValue(TextFileShareMode.ReadWrite)]
        public TextFileShareMode ShareMode { get; set; } = TextFileShareMode.ReadWrite;

        protected override void Execute(CodeActivityContext context)
        {
            var filePath = FileActivityHelper.RequirePath(FilePath, context, nameof(FilePath));
            var content = FileActivityHelper.GetOrDefault(Content, context, string.Empty) ?? string.Empty;
            var encodingName = FileActivityHelper.GetOrDefault(Encoding, context, "UTF-8");
            var append = FileActivityHelper.GetOrDefault(Append, context, false);
            var encoding = FileActivityHelper.ResolveEncoding(encodingName);
            var share = FileActivityHelper.ResolveFileShare(ShareMode);

            FileActivityHelper.EnsureParentDirectoryExists(filePath);
            FileActivityHelper.WriteAllText(filePath, content, encoding, share, append);
        }
    }
}
