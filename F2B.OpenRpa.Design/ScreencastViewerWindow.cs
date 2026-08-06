using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace F2B.OpenRpa.Design
{
    /// <summary>
    /// Screencast viewer: zoom / pan / scrollbars.
    /// Right-click image for Change image / Delete image.
    /// </summary>
    public sealed class ScreencastViewerWindow : Window
    {
        private readonly ScrollViewer _scrollViewer;
        private readonly Image _image;
        private readonly ScaleTransform _scale;
        private readonly ContextMenu _imageContextMenu;
        private string _uuid;
        private bool _isPanning;
        private Point _panStart;
        private double _panOffsetX;
        private double _panOffsetY;

        public ScreencastViewerWindow(string uuid)
        {
            _uuid = uuid;
            Title = "Screencast";
            Width = 720;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.WhiteSmoke;

            _scale = new ScaleTransform(1, 1);
            _image = new Image
            {
                Stretch = Stretch.None,
                LayoutTransform = _scale,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);

            var copyItem = new MenuItem { Header = "Copy image" };
            copyItem.Click += (_, __) => CopyImage();
            var changeItem = new MenuItem { Header = "Change image" };
            changeItem.Click += (_, __) => ChangeImage();
            var deleteItem = new MenuItem { Header = "Delete image" };
            deleteItem.Click += (_, __) => DeleteImage();
            _imageContextMenu = new ContextMenu();
            _imageContextMenu.Items.Add(copyItem);
            _imageContextMenu.Items.Add(changeItem);
            _imageContextMenu.Items.Add(deleteItem);
            _image.ContextMenu = _imageContextMenu;

            _scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _image,
                Background = Brushes.Transparent
            };

            _scrollViewer.PreviewMouseWheel += OnMouseWheel;
            _scrollViewer.PreviewMouseLeftButtonDown += OnPanStart;
            _scrollViewer.PreviewMouseMove += OnPanMove;
            _scrollViewer.PreviewMouseLeftButtonUp += OnPanEnd;
            _scrollViewer.MouseLeave += (_, __) => EndPan();

            Content = _scrollViewer;
            Loaded += OnLoaded;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        /// <summary>Raised when Screencast uuid changes (new value or null when cleared).</summary>
        public event Action<string> ScreencastChanged;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            if (string.IsNullOrWhiteSpace(_uuid))
            {
                Close();
                return;
            }

            TryLoadCurrentImage();
            Dispatcher.BeginInvoke(new Action(FitImageToViewport), DispatcherPriority.Loaded);
        }

        private bool TryLoadCurrentImage()
        {
            var bitmap = ScreencastImageStore.TryLoadBitmap(_uuid);
            if (bitmap != null)
            {
                _image.Source = bitmap;
                Title = "Screencast";
                return true;
            }

            // Keep viewer open so Change / Delete can repair a migrated missing file.
            _image.Source = ScreencastImageStore.BrokenPlaceholder;
            Title = "Screencast (missing or corrupted)";
            return false;
        }

        private void CopyImage()
        {
            try
            {
                BitmapSource bitmap = _image.Source as BitmapSource;
                if (bitmap == null || ReferenceEquals(bitmap, ScreencastImageStore.BrokenPlaceholder))
                {
                    bitmap = ScreencastImageStore.TryLoadBitmap(_uuid);
                }

                if (bitmap == null)
                {
                    MessageBox.Show(
                        this,
                        "No image available to copy.",
                        "Screencast",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                Clipboard.SetImage(bitmap);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Failed to copy image: " + ex.Message,
                    "Screencast",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ChangeImage()
        {
            string newUuid;
            string error;
            if (!ScreencastPickWindow.TryPickAndSave(this, "Change image", _uuid, out newUuid, out error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    MessageBox.Show(
                        this,
                        error,
                        "Screencast",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                return;
            }

            _uuid = newUuid;
            ScreencastChanged?.Invoke(_uuid);
            // Return to workflow after change — do not stay on / reopen viewer.
            Close();
        }

        private void DeleteImage()
        {
            if (string.IsNullOrWhiteSpace(_uuid))
            {
                return;
            }

            var confirm = MessageBox.Show(
                this,
                "Delete this screencast image? This cannot be undone.",
                "Delete image",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            ScreencastImageStore.TryDelete(_uuid);
            _uuid = null;
            ScreencastChanged?.Invoke(null);
            Close();
        }

        private void FitImageToViewport()
        {
            var source = _image.Source as BitmapSource;
            if (source == null || source.PixelWidth <= 0 || source.PixelHeight <= 0)
            {
                return;
            }

            var viewportW = _scrollViewer.ViewportWidth;
            var viewportH = _scrollViewer.ViewportHeight;
            if (viewportW <= 1 || viewportH <= 1)
            {
                viewportW = Math.Max(0, _scrollViewer.ActualWidth - SystemParameters.VerticalScrollBarWidth);
                viewportH = Math.Max(0, _scrollViewer.ActualHeight - SystemParameters.HorizontalScrollBarHeight);
            }

            if (viewportW <= 1 || viewportH <= 1)
            {
                return;
            }

            var scaleX = viewportW / source.PixelWidth;
            var scaleY = viewportH / source.PixelHeight;
            var scale = Math.Min(scaleX, scaleY);
            if (scale > 1)
            {
                scale = 1;
            }

            if (scale < 0.1)
            {
                scale = 0.1;
            }

            _scale.ScaleX = scale;
            _scale.ScaleY = scale;
            _scrollViewer.ScrollToHorizontalOffset(0);
            _scrollViewer.ScrollToVerticalOffset(0);
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_image.Source == null)
            {
                return;
            }

            var factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
            var next = _scale.ScaleX * factor;
            if (next < 0.1)
            {
                next = 0.1;
            }
            else if (next > 8)
            {
                next = 8;
            }

            _scale.ScaleX = next;
            _scale.ScaleY = next;
            e.Handled = true;
        }

        private void OnPanStart(object sender, MouseButtonEventArgs e)
        {
            if (_image.Source == null || e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            if (IsUnderScrollBar(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (_scrollViewer.ScrollableWidth <= 0 && _scrollViewer.ScrollableHeight <= 0)
            {
                return;
            }

            _isPanning = true;
            _panStart = e.GetPosition(_scrollViewer);
            _panOffsetX = _scrollViewer.HorizontalOffset;
            _panOffsetY = _scrollViewer.VerticalOffset;
            _scrollViewer.CaptureMouse();
            _scrollViewer.Cursor = Cursors.Hand;
            e.Handled = true;
        }

        private static bool IsUnderScrollBar(DependencyObject source)
        {
            var current = source;
            while (current != null)
            {
                if (current is ScrollBar)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void OnPanMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning)
            {
                return;
            }

            var pos = e.GetPosition(_scrollViewer);
            var dx = pos.X - _panStart.X;
            var dy = pos.Y - _panStart.Y;
            _scrollViewer.ScrollToHorizontalOffset(_panOffsetX - dx);
            _scrollViewer.ScrollToVerticalOffset(_panOffsetY - dy);
        }

        private void OnPanEnd(object sender, MouseButtonEventArgs e)
        {
            EndPan();
        }

        private void EndPan()
        {
            if (!_isPanning)
            {
                return;
            }

            _isPanning = false;
            _scrollViewer.ReleaseMouseCapture();
            _scrollViewer.Cursor = Cursors.Arrow;
        }
    }
}
