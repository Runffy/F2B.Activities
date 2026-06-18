using System;
using System.Activities;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(ImagesToPdfDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Images to PDF")]
    [Description("Combine one or more image files into a single PDF. Images are stacked vertically in order, each at its original width and height.")]
    public sealed class ImagesToPdfActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public ImagesToPdfActivity()
        {
            DisplayName = "Images to PDF";
        }

        [RequiredArgument]
        [DisplayName("Image paths")]
        [Description("One or more image file paths as a string array.")]
        [Category("Input.A")]
        public InArgument<string[]> ImagePaths { get; set; }

        [RequiredArgument]
        [DisplayName("Output PDF path")]
        [Category("Input.B")]
        public InArgument<string> OutputPdfPath { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new ImagesToPdfActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            var outputPath = (OutputPdfPath.Get(context) ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output PDF path is required.", nameof(OutputPdfPath));

            var paths = ImagePaths.Get(context);
            if (paths == null || paths.Length == 0)
                throw new InvalidOperationException("At least one image path is required.");

            var imagePaths = ImagesToPdfBuilder.ResolveExistingImagePaths(paths);
            outputPath = PathHelper.ToFullPath(outputPath);
            ImagesToPdfBuilder.Build(outputPath, imagePaths);
        }
    }

    internal static class PathHelper
    {
        public static string ToFullPath(string path)
        {
            return System.IO.Path.GetFullPath(path.Trim());
        }
    }
}
