using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;

namespace F2B.Microsoft.Word
{
    [DisplayName("Append Image")]
    [Description("Append one or more images to the end of a Word document. Creates the document if it does not exist. Multiple image paths can be separated by ';'.")]
    [Designer(typeof(AppendImageActivityDesigner))]
    public sealed class AppendImageActivity : CodeActivity
    {
        public AppendImageActivity()
        {
            DisplayName = "Append Image";
            SizeMode = WordImageSizeMode.RegularSize;
            Unit = WordImageUnit.Cm;
            Visible = false;
        }

        [DisplayName("Word File Path")]
        [Description("Path to the Word document. Creates a new .docx file when the path does not exist.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> WordFilePath { get; set; }

        [DisplayName("Image Path")]
        [Description("One or more image file paths. Separate multiple paths with ';'.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> ImagePath { get; set; }

        [DisplayName("Size Mode")]
        [Description("Regular Size keeps original dimensions. Auto fit scales to printable page area. Custom uses Width/Height.")]
        [Category("Input.C")]
        [DefaultValue(WordImageSizeMode.RegularSize)]
        public WordImageSizeMode SizeMode { get; set; }

        [DisplayName("Width")]
        [Description("Custom width. Used only when Size Mode is Custom.")]
        [Category("Input.D")]
        public InArgument<double> Width { get; set; }

        [DisplayName("Height")]
        [Description("Custom height. Used only when Size Mode is Custom.")]
        [Category("Input.E")]
        public InArgument<double> Height { get; set; }

        [DisplayName("Unit")]
        [Description("Unit for Width and Height when Size Mode is Custom.")]
        [Category("Input.F")]
        [DefaultValue(WordImageUnit.Cm)]
        public WordImageUnit Unit { get; set; }

        [DisplayName("Visible")]
        [Description("Show the Word window when opening or creating a document. Ignored when attaching to an already open document.")]
        [Category("Input.G")]
        [DefaultValue(false)]
        public InArgument<bool> Visible { get; set; } = false;

        protected override void Execute(CodeActivityContext context)
        {
            var wordFilePath = WordActivityHelper.RequireNonEmpty(WordFilePath, context, nameof(WordFilePath));
            var imagePath = WordActivityHelper.RequireNonEmpty(ImagePath, context, nameof(ImagePath));
            var imagePaths = WordActivityHelper.ParseAndValidateImagePaths(imagePath);
            var visible = WordActivityHelper.GetOrDefault(Visible, context, false);

            double width = 0;
            double height = 0;
            if (SizeMode == WordImageSizeMode.Custom)
            {
                width = WordActivityHelper.GetOrDefault(Width, context, 0d);
                height = WordActivityHelper.GetOrDefault(Height, context, 0d);
                if (width <= 0 || height <= 0)
                {
                    throw new ArgumentException("Width and Height must be greater than zero when Size Mode is Custom.");
                }
            }

            WordAppendImageService.AppendImages(
                wordFilePath,
                imagePaths,
                SizeMode,
                width,
                height,
                Unit,
                visible);
        }
    }
}
