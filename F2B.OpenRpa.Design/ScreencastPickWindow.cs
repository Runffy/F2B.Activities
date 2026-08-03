using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace F2B.OpenRpa.Design
{
    /// <summary>
    /// Intermediate picker for Set/Change image: browse file or Ctrl+V clipboard preview, then OK/Cancel.
    /// </summary>
    public sealed class ScreencastPickWindow : Window
    {
        private readonly Image _previewImage;
        private readonly TextBlock _hintLabel;
        private readonly Button _okButton;
        private BitmapSource _pendingImage;

        public ScreencastPickWindow(string title)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Select image" : title;
            Width = 420;
            Height = 360;
            MinWidth = 360;
            MinHeight = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.WhiteSmoke;
            Focusable = true;

            var browseButton = new Button
            {
                Content = "Browse from file",
                Padding = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 10)
            };
            browseButton.Click += (_, __) => BrowseFromFile();

            _hintLabel = new TextBlock
            {
                Text = "No image — Browse from file, or Ctrl+V to paste",
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(12)
            };

            _previewImage = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(4),
                Visibility = Visibility.Collapsed
            };

            var previewHost = new Grid();
            previewHost.Children.Add(_hintLabel);
            previewHost.Children.Add(_previewImage);

            var previewBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                MinHeight = 180,
                Child = previewHost
            };

            _okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Padding = new Thickness(8, 3, 8, 3),
                IsEnabled = false,
                IsDefault = true,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _okButton.Click += (_, __) => Confirm();

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Padding = new Thickness(8, 3, 8, 3),
                IsCancel = true
            };
            cancelButton.Click += (_, __) =>
            {
                DialogResult = false;
                Close();
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            buttons.Children.Add(_okButton);
            buttons.Children.Add(cancelButton);

            var root = new DockPanel { Margin = new Thickness(12) };
            DockPanel.SetDock(browseButton, Dock.Top);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(browseButton);
            root.Children.Add(buttons);
            root.Children.Add(previewBorder);

            Content = root;

            // Allow Ctrl+V even when focus is on a child (e.g. buttons).
            AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown), true);
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, (_, __) => TryPasteFromClipboard()));
            Loaded += (_, __) =>
            {
                Activate();
                Focus();
            };
        }

        /// <summary>Frozen preview image confirmed by OK; null if cancelled.</summary>
        public BitmapSource SelectedImage { get; private set; }

        /// <summary>
        /// Shows the picker; on OK saves under Screens and returns new uuid.
        /// </summary>
        public static bool TryPickAndSave(
            Window owner,
            string title,
            string previousUuid,
            out string newUuid,
            out string error)
        {
            newUuid = null;
            error = null;

            var picker = new ScreencastPickWindow(title);
            if (owner != null)
            {
                picker.Owner = owner;
            }

            var result = picker.ShowDialog();
            if (result != true || picker.SelectedImage == null)
            {
                return false;
            }

            return ScreencastImageStore.TrySaveFromBitmapSource(
                picker.SelectedImage,
                previousUuid,
                out newUuid,
                out error);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                TryPasteFromClipboard();
                e.Handled = true;
            }
        }

        private void BrowseFromFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Browse from file",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                BitmapSource frame;
                using (var stream = File.OpenRead(dialog.FileName))
                {
                    frame = LoadBitmapFromStream(stream);
                }

                SetPreview(frame);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Failed to load image: " + ex.Message,
                    "Screencast",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void TryPasteFromClipboard()
        {
            try
            {
                BitmapSource image;
                string error;
                if (!TryReadClipboardImage(out image, out error))
                {
                    MessageBox.Show(
                        this,
                        string.IsNullOrWhiteSpace(error)
                            ? "Clipboard does not contain an image."
                            : error,
                        "Screencast",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                SetPreview(image);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Failed to paste image: " + ex.Message,
                    "Screencast",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static bool TryReadClipboardImage(out BitmapSource image, out string error)
        {
            image = null;
            error = null;

            IDataObject data;
            try
            {
                data = Clipboard.GetDataObject();
            }
            catch (Exception ex)
            {
                error = "Unable to read clipboard: " + ex.Message;
                return false;
            }

            if (data == null)
            {
                error = "Clipboard is empty.";
                return false;
            }

            // 1) PNG — preferred by many screenshot tools / browsers.
            image = TryLoadClipboardPng(data);
            if (IsValidBitmap(image))
            {
                return true;
            }

            // 2) WPF Bitmap / DIB via Clipboard.GetImage().
            try
            {
                if (Clipboard.ContainsImage())
                {
                    image = Clipboard.GetImage();
                    if (IsValidBitmap(image))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            // 3) Explicit Bitmap format.
            try
            {
                if (data.GetDataPresent(DataFormats.Bitmap, true))
                {
                    image = Clipboard.GetImage();
                    if (IsValidBitmap(image))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            error = "Clipboard does not contain a usable image.";
            image = null;
            return false;
        }

        private static BitmapSource TryLoadClipboardPng(IDataObject data)
        {
            try
            {
                // Format name varies by source ("PNG" is common).
                foreach (var format in new[] { "PNG", "image/png" })
                {
                    if (!data.GetDataPresent(format, false))
                    {
                        continue;
                    }

                    var raw = data.GetData(format, false);
                    var stream = raw as Stream;
                    if (stream != null)
                    {
                        return LoadBitmapFromStream(stream);
                    }

                    var bytes = raw as byte[];
                    if (bytes != null && bytes.Length > 0)
                    {
                        using (var ms = new MemoryStream(bytes))
                        {
                            return LoadBitmapFromStream(ms);
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static BitmapSource LoadBitmapFromStream(Stream stream)
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            // Copy to memory first — some clipboard streams cannot be rewound.
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                ms.Position = 0;
                var decoder = BitmapDecoder.Create(
                    ms,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                return MaterializeBitmap(decoder.Frames[0]);
            }
        }

        private void SetPreview(BitmapSource source)
        {
            if (!IsValidBitmap(source))
            {
                return;
            }

            // Materialize pixels — clipboard InteropBitmap + PNG re-encode often becomes blank white.
            var materialized = MaterializeBitmap(source);
            if (!IsValidBitmap(materialized))
            {
                MessageBox.Show(
                    this,
                    "Failed to decode the pasted image.",
                    "Screencast",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _pendingImage = materialized;
            _previewImage.Source = materialized;
            _previewImage.Visibility = Visibility.Visible;
            _hintLabel.Visibility = Visibility.Collapsed;
            _okButton.IsEnabled = true;
        }

        private void Confirm()
        {
            if (_pendingImage == null)
            {
                return;
            }

            SelectedImage = _pendingImage;
            DialogResult = true;
            Close();
        }

        private static bool IsValidBitmap(BitmapSource source)
        {
            return source != null && source.PixelWidth > 0 && source.PixelHeight > 0;
        }

        /// <summary>
        /// Copies pixels into a frozen Bgra32 bitmap. Avoids blank previews from clipboard InteropBitmap
        /// (PNG encode/decode round-trip often yields an all-white image).
        /// </summary>
        private static BitmapSource MaterializeBitmap(BitmapSource source)
        {
            if (source == null)
            {
                return null;
            }

            BitmapSource prepared = source;
            if (prepared.Format != PixelFormats.Bgra32)
            {
                prepared = new FormatConvertedBitmap(prepared, PixelFormats.Bgra32, null, 0);
            }

            var width = prepared.PixelWidth;
            var height = prepared.PixelHeight;
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            var stride = width * 4;
            var pixels = new byte[stride * height];
            prepared.CopyPixels(pixels, stride, 0);

            var result = BitmapSource.Create(
                width,
                height,
                prepared.DpiX > 0 ? prepared.DpiX : 96,
                prepared.DpiY > 0 ? prepared.DpiY : 96,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride);
            result.Freeze();
            return result;
        }
    }
}
