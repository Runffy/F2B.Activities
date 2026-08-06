using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace F2B.OpenRpa.Design
{
    /// <summary>
    /// Pick an existing Screens\{uuid}.png: left = uuid list, right = zoomable preview.
    /// </summary>
    public sealed class ScreencastExistingWindow : Window
    {
        private readonly ListBox _list;
        private readonly ScrollViewer _scrollViewer;
        private readonly Image _image;
        private readonly ScaleTransform _scale;
        private readonly Button _okButton;
        private readonly TextBlock _emptyLabel;
        private double _zoom = 1.0;

        public ScreencastExistingWindow()
        {
            Title = "From existing";
            Width = 720;
            Height = 480;
            MinWidth = 560;
            MinHeight = 360;
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

            _emptyLabel = new TextBlock
            {
                Text = "Select a uuid on the left",
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12)
            };

            var previewHost = new Grid();
            previewHost.Children.Add(_emptyLabel);
            previewHost.Children.Add(_image);

            _scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = previewHost,
                Background = Brushes.White
            };
            _scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;

            _list = new ListBox
            {
                MinWidth = 180,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            _list.SelectionChanged += OnSelectionChanged;

            var zoomOut = new Button { Content = "-", Width = 28, Margin = new Thickness(0, 0, 4, 0) };
            zoomOut.Click += (_, __) => SetZoom(_zoom / 1.2);
            var zoomIn = new Button { Content = "+", Width = 28, Margin = new Thickness(0, 0, 4, 0) };
            zoomIn.Click += (_, __) => SetZoom(_zoom * 1.2);
            var zoomFit = new Button { Content = "Fit", Width = 40, Margin = new Thickness(0, 0, 8, 0) };
            zoomFit.Click += (_, __) => FitToViewport();

            var zoomBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6)
            };
            zoomBar.Children.Add(zoomOut);
            zoomBar.Children.Add(zoomIn);
            zoomBar.Children.Add(zoomFit);

            var rightPanel = new DockPanel();
            DockPanel.SetDock(zoomBar, Dock.Top);
            rightPanel.Children.Add(zoomBar);
            rightPanel.Children.Add(_scrollViewer);

            var split = new Grid();
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(_list, 0);
            Grid.SetColumn(rightPanel, 1);
            _list.Margin = new Thickness(0, 0, 8, 0);
            split.Children.Add(_list);
            split.Children.Add(rightPanel);

            _okButton = new Button
            {
                Content = "OK",
                Width = 80,
                IsEnabled = false,
                IsDefault = true,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _okButton.Click += (_, __) => Confirm();

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
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
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);
            root.Children.Add(split);
            Content = root;

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                    Close();
                    e.Handled = true;
                }
            };

            Loaded += (_, __) => LoadUuidList();
        }

        /// <summary>Selected Screens uuid (no extension), or null when cancelled.</summary>
        public string SelectedUuid { get; private set; }

        public static bool TryPickExisting(Window owner, out string uuid, out string error)
        {
            uuid = null;
            error = null;

            var window = new ScreencastExistingWindow();
            if (owner != null)
            {
                window.Owner = owner;
            }

            if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.SelectedUuid))
            {
                return false;
            }

            uuid = window.SelectedUuid;
            return true;
        }

        private void LoadUuidList()
        {
            _list.Items.Clear();
            string screensDir;
            string error;
            if (!OpenRpaProjectPaths.TryGetScreensDirectory(out screensDir, out error))
            {
                MessageBox.Show(
                    this,
                    string.IsNullOrWhiteSpace(error) ? "Screens directory is unavailable." : error,
                    "From existing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(screensDir))
            {
                return;
            }

            List<string> uuids = Directory.EnumerateFiles(screensDir, "*.png", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string id in uuids)
            {
                _list.Items.Add(id);
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string uuid = _list.SelectedItem as string;
            _okButton.IsEnabled = !string.IsNullOrWhiteSpace(uuid);
            if (string.IsNullOrWhiteSpace(uuid))
            {
                _image.Source = null;
                _emptyLabel.Visibility = Visibility.Visible;
                return;
            }

            var bitmap = ScreencastImageStore.TryLoadBitmap(uuid);
            if (bitmap == null)
            {
                _image.Source = ScreencastImageStore.BrokenPlaceholder;
                _emptyLabel.Visibility = Visibility.Collapsed;
                return;
            }

            _image.Source = bitmap;
            _emptyLabel.Visibility = Visibility.Collapsed;
            SetZoom(1.0);
            Dispatcher.BeginInvoke(new Action(FitToViewport), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void Confirm()
        {
            string uuid = _list.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(uuid))
            {
                return;
            }

            SelectedUuid = uuid;
            DialogResult = true;
            Close();
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            {
                return;
            }

            SetZoom(e.Delta > 0 ? _zoom * 1.1 : _zoom / 1.1);
            e.Handled = true;
        }

        private void SetZoom(double zoom)
        {
            _zoom = Math.Max(0.1, Math.Min(8.0, zoom));
            _scale.ScaleX = _zoom;
            _scale.ScaleY = _zoom;
        }

        private void FitToViewport()
        {
            var source = _image.Source as BitmapSource;
            if (source == null || source.PixelWidth <= 0 || source.PixelHeight <= 0)
            {
                return;
            }

            double viewportW = Math.Max(1, _scrollViewer.ViewportWidth);
            double viewportH = Math.Max(1, _scrollViewer.ViewportHeight);
            if (viewportW <= 1 || viewportH <= 1)
            {
                viewportW = Math.Max(1, _scrollViewer.ActualWidth);
                viewportH = Math.Max(1, _scrollViewer.ActualHeight);
            }

            double scale = Math.Min(viewportW / source.PixelWidth, viewportH / source.PixelHeight);
            SetZoom(Math.Max(0.1, Math.Min(1.0, scale)));
        }
    }
}
