using System;
using System.Activities.Presentation.Model;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace F2B.OpenRpa.Design
{
    /// <summary>
    /// Design-time screencast control on the workflow canvas.
    /// No image: centered "Set image" button.
    /// Has image: centered thumbnail only (click opens viewer).
    /// </summary>
    public sealed class ScreencastThumbnailControl : Grid
    {
        public const double FixedWidth = 160;
        public const double MaxImageHeight = 90;
        private const double BrokenFixedWidth = FixedWidth / 2;
        private const double BrokenMaxImageHeight = MaxImageHeight / 2;
        private const double ButtonWidth = 96;
        private const int MaxLoadRetries = 8;

        private static readonly int[] RetryDelaysMs = { 50, 100, 200, 400, 800, 1200, 2000, 3000 };

        private readonly StackPanel _noImagePanel;
        private readonly Button _thumbButton;
        private readonly Image _image;
        private readonly Button _setImageButton;

        private ModelItem _modelItem;
        private string _propertyName = "Screencast";
        private int _loadRetryCount;
        private string _retryUuid;
        private DispatcherTimer _retryTimer;

        public ScreencastThumbnailControl()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            Margin = new Thickness(0, 0, 0, 6);

            _setImageButton = new Button
            {
                Content = "Set image",
                Width = ButtonWidth,
                Padding = new Thickness(4, 2, 4, 2),
                Focusable = false
            };
            _setImageButton.Click += (_, __) => SetImage();

            _noImagePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _noImagePanel.Children.Add(_setImageButton);

            _image = new Image
            {
                Stretch = Stretch.Uniform,
                MaxWidth = FixedWidth - 2,
                MaxHeight = MaxImageHeight,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            _thumbButton = new Button
            {
                Width = FixedWidth,
                MinHeight = 24,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Padding = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Focusable = false,
                ToolTip = "Click to view image",
                Content = _image,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _thumbButton.Click += (_, __) => ViewImage();

            Children.Add(_noImagePanel);
            Children.Add(_thumbButton);

            Loaded += OnControlLoaded;
            IsVisibleChanged += OnIsVisibleChanged;

            ShowNoImageState();
        }

        public void Attach(ModelItem modelItem, string propertyName = "Screencast")
        {
            if (_modelItem != null)
            {
                _modelItem.PropertyChanged -= OnModelPropertyChanged;
            }

            _modelItem = modelItem;
            _propertyName = string.IsNullOrWhiteSpace(propertyName) ? "Screencast" : propertyName;
            _loadRetryCount = 0;
            _retryUuid = null;
            StopRetryTimer();

            if (_modelItem != null)
            {
                _modelItem.PropertyChanged += OnModelPropertyChanged;
            }

            Refresh();
        }

        public void Detach()
        {
            StopRetryTimer();
            if (_modelItem != null)
            {
                _modelItem.PropertyChanged -= OnModelPropertyChanged;
                _modelItem = null;
            }
        }

        public void Refresh()
        {
            RefreshCore(scheduleRetryOnMiss: true);
        }

        private void RefreshCore(bool scheduleRetryOnMiss)
        {
            var uuid = ReadUuid();
            if (string.IsNullOrWhiteSpace(uuid))
            {
                StopRetryTimer();
                _loadRetryCount = 0;
                _retryUuid = null;
                _image.Source = null;
                ShowNoImageState();
                return;
            }

            if (!string.Equals(_retryUuid, uuid, StringComparison.OrdinalIgnoreCase))
            {
                _retryUuid = uuid;
                _loadRetryCount = 0;
            }

            var bitmap = ScreencastImageStore.TryLoadBitmap(uuid);
            if (bitmap != null)
            {
                StopRetryTimer();
                _loadRetryCount = 0;
                ApplyThumbSize(FixedWidth, MaxImageHeight);
                _image.Source = bitmap;
                _thumbButton.ToolTip = "Click to view image";
                ShowHasImageState();
                return;
            }

            // Id is set but PNG not resolved yet (designer/project path race) or truly missing.
            ApplyThumbSize(BrokenFixedWidth, BrokenMaxImageHeight);
            _image.Source = ScreencastImageStore.BrokenPlaceholder;
            _thumbButton.ToolTip = "Image file missing or corrupted. Click to repair.";
            ShowHasImageState();

            if (scheduleRetryOnMiss)
            {
                ScheduleLoadRetry();
            }
        }

        private void ScheduleLoadRetry()
        {
            if (_loadRetryCount >= MaxLoadRetries)
            {
                StopRetryTimer();
                return;
            }

            if (_retryTimer == null)
            {
                _retryTimer = new DispatcherTimer { IsEnabled = false };
                _retryTimer.Tick += OnRetryTimerTick;
            }

            var delayIndex = Math.Min(_loadRetryCount, RetryDelaysMs.Length - 1);
            _retryTimer.Interval = TimeSpan.FromMilliseconds(RetryDelaysMs[delayIndex]);
            _retryTimer.Stop();
            _retryTimer.Start();
        }

        private void OnRetryTimerTick(object sender, EventArgs e)
        {
            StopRetryTimer();
            _loadRetryCount++;
            RefreshCore(scheduleRetryOnMiss: true);
        }

        private void StopRetryTimer()
        {
            if (_retryTimer == null)
            {
                return;
            }

            _retryTimer.Stop();
        }

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            // Designer/project context is often ready only after the visual tree loads.
            if (!string.IsNullOrWhiteSpace(ReadUuid()) &&
                (_image.Source == null || ReferenceEquals(_image.Source, ScreencastImageStore.BrokenPlaceholder)))
            {
                RefreshCore(scheduleRetryOnMiss: true);
            }
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible &&
                !string.IsNullOrWhiteSpace(ReadUuid()) &&
                (_image.Source == null || ReferenceEquals(_image.Source, ScreencastImageStore.BrokenPlaceholder)))
            {
                RefreshCore(scheduleRetryOnMiss: true);
            }
        }

        private void ApplyThumbSize(double width, double maxHeight)
        {
            _thumbButton.Width = width;
            _image.MaxWidth = width - 2;
            _image.MaxHeight = maxHeight;
        }

        private void ShowNoImageState()
        {
            _noImagePanel.Visibility = Visibility.Visible;
            _thumbButton.Visibility = Visibility.Collapsed;
        }

        private void ShowHasImageState()
        {
            _noImagePanel.Visibility = Visibility.Collapsed;
            _thumbButton.Visibility = Visibility.Visible;
        }

        private void SetImage()
        {
            if (!EnsureModelItem())
            {
                return;
            }

            string newUuid;
            string error;
            if (!ScreencastPickWindow.TryPickAndSave(
                    Window.GetWindow(this),
                    "Set image",
                    previousUuid: null,
                    out newUuid,
                    out error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    MessageBox.Show(
                        Window.GetWindow(this),
                        error,
                        "Screencast",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                return;
            }

            WriteUuid(newUuid);
            Refresh();
            // Do not open viewer after set — return to the workflow canvas.
        }

        private void ViewImage()
        {
            if (!EnsureModelItem())
            {
                return;
            }

            var uuid = ReadUuid();
            if (string.IsNullOrWhiteSpace(uuid))
            {
                return;
            }

            OpenViewer(uuid);
        }

        private void OpenViewer(string uuid)
        {
            var owner = Window.GetWindow(this);
            var viewer = new ScreencastViewerWindow(uuid);
            if (owner != null)
            {
                viewer.Owner = owner;
            }

            viewer.ScreencastChanged += OnViewerScreencastChanged;
            viewer.ShowDialog();
            viewer.ScreencastChanged -= OnViewerScreencastChanged;
            Refresh();
        }

        private void OnViewerScreencastChanged(string newUuid)
        {
            WriteUuid(newUuid);
        }

        private bool EnsureModelItem()
        {
            if (_modelItem != null)
            {
                return true;
            }

            MessageBox.Show(
                Window.GetWindow(this),
                "Screencast is not bound to the activity yet. Select the activity and try again.",
                "Screencast",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        private void OnModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                string.Equals(e.PropertyName, _propertyName, StringComparison.OrdinalIgnoreCase))
            {
                _loadRetryCount = 0;
                Refresh();
            }
        }

        private string ReadUuid()
        {
            var property = _modelItem?.Properties[_propertyName];
            if (property == null)
            {
                return null;
            }

            var computed = property.ComputedValue as string;
            if (!string.IsNullOrWhiteSpace(computed))
            {
                return computed;
            }

            var valueItem = property.Value;
            if (valueItem == null)
            {
                return null;
            }

            return valueItem.GetCurrentValue() as string;
        }

        private void WriteUuid(string uuid)
        {
            if (_modelItem?.Properties[_propertyName] == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(uuid))
            {
                _modelItem.Properties[_propertyName].ClearValue();
            }
            else
            {
                _modelItem.Properties[_propertyName].SetValue(uuid);
            }
        }
    }
}
