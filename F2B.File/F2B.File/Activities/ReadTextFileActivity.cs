using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Read Text File")]
    [Description("Read all text from a file.")]
    public sealed class ReadTextFileActivity : CodeActivity
    {
        public ReadTextFileActivity()
        {
            DisplayName = "Read Text File";
        }

        [DisplayName("File Path")]
        [Description("Path of the text file to read.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("Encoding")]
        [Description("Text encoding name, for example UTF-8 or UTF-8 BOM.")]
        [Category("Input.B")]
        [DefaultValue("UTF-8")]
        public InArgument<string> Encoding { get; set; } = "UTF-8";

        [DisplayName("Share Mode")]
        [Description("File sharing mode while the file is open for reading.")]
        [Category("Input.C")]
        [DefaultValue(TextFileShareMode.ReadWrite)]
        public TextFileShareMode ShareMode { get; set; } = TextFileShareMode.ReadWrite;

        [DisplayName("Content")]
        [Description("File text content.")]
        [Category("Output")]
        public OutArgument<string> Content { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var filePath = FileActivityHelper.RequirePath(FilePath, context, nameof(FilePath));
            var encodingName = FileActivityHelper.GetOrDefault(Encoding, context, "UTF-8");
            var encoding = FileActivityHelper.ResolveEncoding(encodingName);
            var share = FileActivityHelper.ResolveFileShare(ShareMode);

            if (!System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException("File was not found.", filePath);
            }

            Content?.Set(context, FileActivityHelper.ReadAllText(filePath, encoding, share));
        }
    }
}
