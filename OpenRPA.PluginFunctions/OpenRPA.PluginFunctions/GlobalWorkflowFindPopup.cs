using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Ctrl+Shift+F: search activities across all projects / workflows.
    /// Works from Open Project view as well as while editing a workflow.
    /// </summary>
    internal static class GlobalWorkflowFindPopup
    {
        private static Popup _popup;
        private static TextBox _searchBox;
        private static ListBox _listBox;
        private static TextBlock _status;
        private static bool _suppressOutsideClose;
        private static Window _hookedWindow;
        private static int _indexVersion;

        internal static Popup CurrentPopup => _popup;

        public static void Show()
        {
            ActivityPalettePopup.Hide();
            WorkflowFindPopup.Hide();

            EnsureUi();
            Window main = PluginContext.MainWindow;
            if (main != null)
            {
                DesignerPopupPlacement.ApplyUpperThird(_popup, main);
                HookWindow(main);
            }

            _searchBox.Text = string.Empty;
            _listBox.ItemsSource = null;
            _status.Text = "Indexing projects…";
            _suppressOutsideClose = true;
            _popup.IsOpen = true;
            _searchBox.Focus();
            Keyboard.Focus(_searchBox);
            PluginContext.RunOnUi(() => _suppressOutsideClose = false);

            int version = ++_indexVersion;
            Task.Run(() =>
            {
                GlobalWorkflowActivitySearch.Invalidate();
                GlobalWorkflowActivitySearch.EnsureIndex();
                return version;
            }).ContinueWith(t =>
            {
                PluginContext.RunOnUi(() =>
                {
                    if (t.Result != _indexVersion || _popup == null || !_popup.IsOpen)
                    {
                        return;
                    }

                    _status.Text = "Find in all projects (↑↓ Enter, Esc closes)";
                    RefreshList(_searchBox != null ? _searchBox.Text : string.Empty);
                });
            }, TaskScheduler.Default);
        }

        public static void Hide()
        {
            if (_popup != null)
            {
                _popup.IsOpen = false;
            }
        }

        private static void EnsureUi()
        {
            if (_popup != null)
            {
                return;
            }

            _searchBox = new TextBox
            {
                MinWidth = 420,
                MaxWidth = 640,
                Height = 28,
                FontSize = 14,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 4)
            };
            _searchBox.TextChanged += (s, e) => RefreshList(_searchBox.Text);
            _searchBox.PreviewKeyDown += OnSearchPreviewKeyDown;

            _listBox = new ListBox
            {
                MinWidth = 420,
                MaxWidth = 640,
                MaxHeight = 320,
                FontSize = 13
            };
            _listBox.ItemTemplate = CreateItemTemplate();
            _listBox.PreviewMouseLeftButtonUp += OnListPreviewMouseLeftButtonUp;
            _listBox.PreviewKeyDown += OnListPreviewKeyDown;

            _status = new TextBlock
            {
                Text = "Find in all projects (↑↓ Enter, Esc closes)",
                FontSize = 11,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var panel = new StackPanel();
            panel.Children.Add(_status);
            panel.Children.Add(_searchBox);
            panel.Children.Add(_listBox);

            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Child = panel,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 8,
                    ShadowDepth = 2,
                    Opacity = 0.35
                }
            };

            _popup = new Popup
            {
                Child = border,
                StaysOpen = true,
                AllowsTransparency = true
            };

            Window main = PluginContext.MainWindow;
            if (main != null)
            {
                DesignerPopupPlacement.ApplyUpperThird(_popup, main);
                HookWindow(main);
            }
        }

        private static DataTemplate CreateItemTemplate()
        {
            var row = new FrameworkElementFactory(typeof(StackPanel));
            row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            row.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 2, 2, 2));

            var icon = new FrameworkElementFactory(typeof(Image));
            icon.SetBinding(Image.SourceProperty, new Binding("Icon"));
            icon.SetValue(FrameworkElement.WidthProperty, 16.0);
            icon.SetValue(FrameworkElement.HeightProperty, 16.0);
            icon.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 8, 0));
            icon.SetValue(Image.StretchProperty, Stretch.Uniform);
            icon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);

            var texts = new FrameworkElementFactory(typeof(StackPanel));
            texts.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            texts.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            var name = new FrameworkElementFactory(typeof(TextBlock));
            name.SetBinding(TextBlock.TextProperty, new Binding("DisplayName"));
            name.SetValue(TextBlock.FontSizeProperty, 13.0);

            var hint = new FrameworkElementFactory(typeof(TextBlock));
            hint.SetBinding(TextBlock.TextProperty, new Binding("MatchHint"));
            hint.SetValue(TextBlock.FontSizeProperty, 11.0);
            hint.SetValue(TextBlock.ForegroundProperty, Brushes.Gray);
            hint.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 1, 0, 0));

            texts.AppendChild(name);
            texts.AppendChild(hint);
            row.AppendChild(icon);
            row.AppendChild(texts);

            return new DataTemplate { VisualTree = row };
        }

        private static void HookWindow(Window window)
        {
            if (window == null || ReferenceEquals(_hookedWindow, window))
            {
                if (window != null)
                {
                    DesignerPopupLifetime.Hook(window, Hide);
                }

                return;
            }

            if (_hookedWindow != null)
            {
                _hookedWindow.PreviewMouseDown -= OnWindowPreviewMouseDown;
                DesignerPopupLifetime.Unhook(_hookedWindow, Hide);
            }

            _hookedWindow = window;
            _hookedWindow.PreviewMouseDown += OnWindowPreviewMouseDown;
            DesignerPopupLifetime.Hook(window, Hide);
        }

        private static void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_popup == null || !_popup.IsOpen || _suppressOutsideClose)
            {
                return;
            }

            DependencyObject source = e.OriginalSource as DependencyObject;
            if (IsInsidePopup(source))
            {
                return;
            }

            Hide();
        }

        private static bool IsInsidePopup(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null)
            {
                if (ReferenceEquals(current, _popup.Child))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current)
                    ?? LogicalTreeHelper.GetParent(current);
            }

            return false;
        }

        private static void RefreshList(string pattern)
        {
            if (_listBox == null)
            {
                return;
            }

            string needle = (pattern ?? string.Empty).Trim();
            if (needle.Length == 0)
            {
                _listBox.ItemsSource = null;
                if (_status != null && _popup != null && _popup.IsOpen)
                {
                    _status.Text = "Type to search all projects (↑↓ Enter, Esc closes)";
                }

                return;
            }

            List<GlobalFindItem> items = GlobalWorkflowActivitySearch.Search(needle).ToList();
            _listBox.ItemsSource = items;
            if (items.Count > 0)
            {
                _listBox.SelectedIndex = 0;
            }

            if (_status != null)
            {
                _status.Text = items.Count == 0
                    ? "No matches"
                    : ("Find in all projects — " + items.Count + " result(s)");
            }
        }

        private static void OnSearchPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Hide();
                return;
            }

            if (e.Key == Key.Down)
            {
                e.Handled = true;
                if (_listBox.Items.Count > 0)
                {
                    _listBox.Focus();
                    if (_listBox.SelectedIndex < 0)
                    {
                        _listBox.SelectedIndex = 0;
                    }

                    var item = _listBox.ItemContainerGenerator.ContainerFromIndex(_listBox.SelectedIndex) as ListBoxItem;
                    if (item != null)
                    {
                        item.Focus();
                    }
                }

                return;
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ConfirmSelection();
            }
        }

        private static void OnListPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Hide();
                return;
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ConfirmSelection();
                return;
            }

            if (e.Key == Key.Up && _listBox.SelectedIndex <= 0)
            {
                e.Handled = true;
                _searchBox.Focus();
                Keyboard.Focus(_searchBox);
            }
        }

        private static void OnListPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || e.ClickCount != 1)
            {
                return;
            }

            ListBoxItem item = FindListBoxItem(e.OriginalSource as DependencyObject);
            if (item == null)
            {
                return;
            }

            item.IsSelected = true;
            e.Handled = true;
            ConfirmSelection();
        }

        private static ListBoxItem FindListBoxItem(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null)
            {
                var item = current as ListBoxItem;
                if (item != null)
                {
                    return item;
                }

                current = VisualTreeHelper.GetParent(current)
                    ?? LogicalTreeHelper.GetParent(current);
            }

            return null;
        }

        private static void ConfirmSelection()
        {
            var selected = _listBox != null ? _listBox.SelectedItem as GlobalFindItem : null;
            if (selected == null || selected.Entry == null)
            {
                return;
            }

            Hide();
            GlobalWorkflowFindNavigator.Navigate(selected.Entry);
        }
    }
}
