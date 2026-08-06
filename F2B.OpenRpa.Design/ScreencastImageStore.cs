using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace F2B.OpenRpa.Design
{
    /// <summary>
    /// Design-time screencast PNG store under project Screens folder (uuid only in activity property).
    /// </summary>
    public static class ScreencastImageStore
    {
        private static ImageSource _brokenPlaceholder;

        /// <summary>
        /// Default icon shown when Screencast id is set but the PNG is missing or unreadable.
        /// </summary>
        public static ImageSource BrokenPlaceholder
        {
            get
            {
                if (_brokenPlaceholder == null)
                {
                    _brokenPlaceholder = CreateBrokenPlaceholder();
                }

                return _brokenPlaceholder;
            }
        }

        public static string NewUuid()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Saves <paramref name="bitmap"/> as a new uuid.png under Screens.
        /// Deletes <paramref name="previousUuid"/> file when different.
        /// </summary>
        public static bool TrySaveFromBitmapSource(
            BitmapSource bitmap,
            string previousUuid,
            out string newUuid,
            out string error)
        {
            newUuid = null;
            error = null;

            if (bitmap == null)
            {
                error = "Image is empty.";
                return false;
            }

            string screensDir;
            if (!OpenRpaProjectPaths.TryGetScreensDirectory(out screensDir, out error))
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(screensDir);
                newUuid = NewUuid();
                var destPath = Path.Combine(screensDir, newUuid + ".png");
                SaveBitmapAsPng(bitmap, destPath);

                if (!string.IsNullOrWhiteSpace(previousUuid) &&
                    !string.Equals(previousUuid, newUuid, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(previousUuid);
                }

                return true;
            }
            catch (Exception ex)
            {
                newUuid = null;
                error = ex.Message;
                return false;
            }
        }

        public static bool TryResolvePath(string uuid, out string fullPath, out string error)
        {
            return OpenRpaProjectPaths.TryResolveScreencastFile(uuid, out fullPath, out error);
        }

        public static BitmapImage TryLoadBitmap(string uuid)
        {
            string path;
            string error;
            if (!TryResolvePath(uuid, out path, out error) || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                // Load via memory so we do not keep a file lock, and avoid UriSource races.
                var bytes = File.ReadAllBytes(path);
                using (var stream = new MemoryStream(bytes))
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Copies/converts <paramref name="sourceFilePath"/> to a new uuid.png under Screens.
        /// Deletes <paramref name="previousUuid"/> file when different.
        /// </summary>
        public static bool TrySaveFromFile(string sourceFilePath, string previousUuid, out string newUuid, out string error)
        {
            newUuid = null;
            error = null;

            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                error = "Selected image file was not found.";
                return false;
            }

            string screensDir;
            if (!OpenRpaProjectPaths.TryGetScreensDirectory(out screensDir, out error))
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(screensDir);
                newUuid = NewUuid();
                var destPath = Path.Combine(screensDir, newUuid + ".png");
                SaveAsPng(sourceFilePath, destPath);

                if (!string.IsNullOrWhiteSpace(previousUuid) &&
                    !string.Equals(previousUuid, newUuid, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(previousUuid);
                }

                return true;
            }
            catch (Exception ex)
            {
                newUuid = null;
                error = ex.Message;
                return false;
            }
        }

        public static bool TryDelete(string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
            {
                return false;
            }

            string path;
            string error;
            if (!TryResolvePath(uuid, out path, out error) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SaveAsPng(string sourceFilePath, string destPath)
        {
            BitmapSource frame;
            using (var stream = File.OpenRead(sourceFilePath))
            {
                var decoder = BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                frame = decoder.Frames[0];
            }

            SaveBitmapAsPng(frame, destPath);
        }

        private static void SaveBitmapAsPng(BitmapSource bitmap, string destPath)
        {
            var frame = bitmap as BitmapFrame ?? BitmapFrame.Create(bitmap);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(frame);
            using (var output = File.Create(destPath))
            {
                encoder.Save(output);
            }
        }

        private static ImageSource CreateBrokenPlaceholder()
        {
            const double size = 64;
            var group = new DrawingGroup();

            var frameBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
            var borderBrush = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));
            var accentBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x40, 0x40));
            var iconBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            frameBrush.Freeze();
            borderBrush.Freeze();
            accentBrush.Freeze();
            iconBrush.Freeze();

            group.Children.Add(new GeometryDrawing(
                frameBrush,
                new Pen(borderBrush, 1.5),
                new RectangleGeometry(new Rect(2, 2, size - 4, size - 4), 3, 3)));

            // Classic "image" silhouette (sun + mountain).
            group.Children.Add(new GeometryDrawing(
                iconBrush,
                null,
                new EllipseGeometry(new Point(20, 20), 5, 5)));

            var mountain = new PathGeometry();
            mountain.Figures.Add(new PathFigure
            {
                StartPoint = new Point(10, 48),
                IsClosed = true,
                Segments =
                {
                    new LineSegment(new Point(26, 28), true),
                    new LineSegment(new Point(36, 38), true),
                    new LineSegment(new Point(46, 24), true),
                    new LineSegment(new Point(54, 48), true)
                }
            });
            mountain.Freeze();
            group.Children.Add(new GeometryDrawing(iconBrush, null, mountain));

            // Crack / X overlay to indicate missing or corrupted source.
            var crackPen = new Pen(accentBrush, 2.5)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            crackPen.Freeze();
            group.Children.Add(new GeometryDrawing(
                null,
                crackPen,
                new LineGeometry(new Point(14, 14), new Point(50, 50))));
            group.Children.Add(new GeometryDrawing(
                null,
                crackPen,
                new LineGeometry(new Point(50, 14), new Point(14, 50))));

            group.Freeze();
            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }
    }
}
