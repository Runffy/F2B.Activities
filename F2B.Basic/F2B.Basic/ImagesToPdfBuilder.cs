using System;
using System.Collections.Generic;
using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace F2B.Basic
{
    internal static class ImagesToPdfBuilder
    {
        public static int Build(string outputPdfPath, IEnumerable<string> imagePaths)
        {
            if (string.IsNullOrWhiteSpace(outputPdfPath))
                throw new ArgumentException("Output PDF path is required.", nameof(outputPdfPath));

            var resolvedPaths = ResolveExistingImagePaths(imagePaths);
            if (resolvedPaths.Count == 0)
                throw new InvalidOperationException("At least one image path is required.");

            var document = new PdfDocument();
            document.Info.Title = Path.GetFileNameWithoutExtension(outputPdfPath) ?? "Images";

            var page = document.AddPage();
            var images = new List<XImage>();
            double maxWidth = 0;
            double totalHeight = 0;

            try
            {
                foreach (var path in resolvedPaths)
                {
                    var image = XImage.FromFile(path);
                    images.Add(image);
                    maxWidth = Math.Max(maxWidth, image.PixelWidth);
                    totalHeight += image.PixelHeight;
                }

                page.Width = maxWidth;
                page.Height = totalHeight;

                using (var graphics = XGraphics.FromPdfPage(page))
                {
                    double offsetY = 0;
                    foreach (var image in images)
                    {
                        graphics.DrawImage(image, 0, offsetY, image.PixelWidth, image.PixelHeight);
                        offsetY += image.PixelHeight;
                    }
                }

                var outputDirectory = Path.GetDirectoryName(outputPdfPath);
                if (!string.IsNullOrEmpty(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                document.Save(outputPdfPath);
            }
            finally
            {
                foreach (var image in images)
                    image.Dispose();

                document.Dispose();
            }

            return resolvedPaths.Count;
        }

        public static IList<string> ResolveExistingImagePaths(IEnumerable<string> imagePaths)
        {
            var resolved = new List<string>();
            if (imagePaths == null)
                return resolved;

            foreach (var rawPath in imagePaths)
            {
                if (string.IsNullOrWhiteSpace(rawPath))
                    continue;

                var fullPath = Path.GetFullPath(rawPath.Trim());
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException("Image file was not found.", fullPath);

                resolved.Add(fullPath);
            }

            return resolved;
        }
    }
}
