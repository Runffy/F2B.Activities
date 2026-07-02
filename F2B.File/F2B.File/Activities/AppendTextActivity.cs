using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Append Text")]
    [Description("Append text content to the end of a text file. Multi-line content is supported.")]
    public sealed class AppendTextActivity : CodeActivity
    {
        public AppendTextActivity()
        {
            DisplayName = "Append Text";
        }

        [DisplayName("File Path")]
        [Description("Path to the text file.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("Content")]
        [Description("Text content to append.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> Content { get; set; }

        [DisplayName("Encoding")]
        [Description("Text encoding name, for example UTF-8 or UTF-8 BOM.")]
        [Category("Input.C")]
        [DefaultValue("UTF-8")]
        public InArgument<string> Encoding { get; set; } = "UTF-8";

        [DisplayName("Share Mode")]
        [Description("File sharing mode while the file is open for writing.")]
        [Category("Input.D")]
        [DefaultValue(TextFileShareMode.ReadWrite)]
        public TextFileShareMode ShareMode { get; set; } = TextFileShareMode.ReadWrite;

        protected override void Execute(CodeActivityContext context)
        {
            var filePath = FileActivityHelper.RequirePath(FilePath, context, nameof(FilePath));
            var content = FileActivityHelper.GetOrDefault(Content, context, string.Empty) ?? string.Empty;
            var encodingName = FileActivityHelper.GetOrDefault(Encoding, context, "UTF-8");
            var encoding = FileActivityHelper.ResolveEncoding(encodingName);
            var share = FileActivityHelper.ResolveFileShare(ShareMode);

            FileActivityHelper.EnsureParentDirectoryExists(filePath);
            FileActivityHelper.WriteAllText(filePath, content, encoding, share, append: true);
        }
    }
}
