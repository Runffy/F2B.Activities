using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using InteropWord = Microsoft.Office.Interop.Word;

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
        }

        [DisplayName("Word File Path")]
        [Description("Path to the Word document. Creates a new .docx file when the path does not exist.")]
        [Category("Input.A")]
        public InArgument<string> WordFilePath { get; set; }

        [DisplayName("Document")]
        [Description("Word Document InOut. When WordFilePath is set, the opened/attached document is assigned here.")]
        [Category("Input.B")]
        public InOutArgument<InteropWord.Document> Document { get; set; }

        [DisplayName("Image Path")]
        [Description("One or more image file paths. Separate multiple paths with ';'.")]
        [RequiredArgument]
        [Category("Input.C")]
        public InArgument<string> ImagePath { get; set; }

        [DisplayName("Size Mode")]
        [Category("Input.D")]
        [DefaultValue(WordImageSizeMode.RegularSize)]
        public WordImageSizeMode SizeMode { get; set; }

        [DisplayName("Width")]
        [Category("Input.E")]
        public InArgument<double> Width { get; set; }

        [DisplayName("Height")]
        [Category("Input.F")]
        public InArgument<double> Height { get; set; }

        [DisplayName("Unit")]
        [Category("Input.G")]
        [DefaultValue(WordImageUnit.Cm)]
        public WordImageUnit Unit { get; set; }

        [DisplayName("Visible")]
        [Category("Input.H")]
        [DefaultValue(false)]
        public InArgument<bool> Visible { get; set; } = false;

        protected override void Execute(CodeActivityContext context)
        {
            var path = WordActivityHelper.GetOptionalPath(WordFilePath, context);
            var existing = WordActivityHelper.GetOptionalDocument(Document, context);
            var imagePath = WordActivityHelper.RequireNonEmpty(ImagePath, context, nameof(ImagePath));
            var imagePaths = WordActivityHelper.ParseAndValidateImagePaths(imagePath);
            var visible = WordActivityHelper.GetOrDefault(Visible, context, false);
            var documentBound = WordActivityHelper.IsBound(Document);

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

            using (var session = WordDocumentSession.Acquire(path, existing, visible, createIfMissing: true, documentBound))
            {
                WordDocumentOperations.AppendImages(session.Document, imagePaths, SizeMode, width, height, Unit);
                WordActivityHelper.SetDocument(Document, context, session.Document);
                session.Complete();
            }
        }
    }
}
