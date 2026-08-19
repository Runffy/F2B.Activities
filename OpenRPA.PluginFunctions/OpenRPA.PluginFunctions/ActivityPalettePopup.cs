using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Ctrl+P palette: filter toolbox activities and insert via AddActivity.
    /// </summary>
    internal static class ActivityPalettePopup
    {
        private static Popup _popup;
        private static TextBox _searchBox;
        private static ListBox _listBox;
        private static bool _suppressOutsideClose;
        private static Window _hookedWindow;

        internal static Popup CurrentPopup => _popup;

        public static void Show()
        {
            ShowCore(true);
        }

        public static void ShowAtSpacer(DependencyObject spacerSource)
        {
            IDesigner designer = PluginContext.ResolveDesigner();
            if (designer == null)
            {
                return;
            }

            ActivityInsertService.CapturePaletteInsertAnchorFrom(spacerSource);
            if (_popup != null && _popup.IsOpen)
            {
                if (_searchBox != null)
                {
                    _searchBox.Focus();
                    Keyboard.Focus(_searchBox);
                }

                return;
            }

            ShowCore(false);
        }

        private static void ShowCore(bool captureAnchor)
        {
            IDesigner designer = PluginContext.ResolveDesigner();
            if (designer == null)
            {
                return;
            }

            if (captureAnchor && (_popup == null || !_popup.IsOpen))
            {
                ActivityInsertService.CapturePaletteInsertAnchor();
            }

            WorkflowFindPopup.Hide();
            GlobalWorkflowFindPopup.Hide();

            EnsureUi();
            Window main = PluginContext.MainWindow;
            if (main != null)
            {
                DesignerPopupPlacement.ApplyUpperThird(_popup, main);
                HookWindow(main);
            }

            RefreshList(string.Empty);
            _suppressOutsideClose = true;
            _popup.IsOpen = true;
            _searchBox.Text = string.Empty;
            _searchBox.Focus();
            Keyboard.Focus(_searchBox);
            PluginContext.RunOnUi(() => _suppressOutsideClose = false);
        }

        public static void Hide()
        {
            ActivityInsertService.ClearPaletteInsertAnchor();
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
                MinWidth = 360,
                MaxWidth = 520,
                Height = 28,
                FontSize = 14,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 4)
            };
            // Activity names are Latin; keep IME off so pinyin doesn't eat the first keystrokes.
            DisableIme(_searchBox);
            _searchBox.GotKeyboardFocus += (s, e) => DisableIme(_searchBox);
            _searchBox.TextChanged += (s, e) => RefreshList(_searchBox.Text);
            _searchBox.PreviewKeyDown += OnSearchPreviewKeyDown;

            _listBox = new ListBox
            {
                MinWidth = 360,
                MaxWidth = 520,
                MaxHeight = 280,
                FontSize = 13
            };
            _listBox.ItemTemplate = CreateItemTemplate();
            _listBox.PreviewMouseLeftButtonUp += OnListPreviewMouseLeftButtonUp;
            _listBox.PreviewKeyDown += OnListPreviewKeyDown;

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "Add activity (↑↓ Enter, Esc closes)",
                FontSize = 11,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 0, 0, 4)
            });
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

            var library = new FrameworkElementFactory(typeof(TextBlock));
            library.SetBinding(TextBlock.TextProperty, new Binding("LibraryName"));
            library.SetValue(TextBlock.FontSizeProperty, 11.0);
            library.SetValue(TextBlock.ForegroundProperty, Brushes.Gray);
            library.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 1, 0, 0));

            texts.AppendChild(name);
            texts.AppendChild(library);
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

            if (ActivityInsertService.IsSequenceSpacer(source))
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

            List<ActivityCatalogItem> items = ActivityCatalog.Search(pattern).ToList();
            _listBox.ItemsSource = items;
            if (items.Count > 0)
            {
                _listBox.SelectedIndex = 0;
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

        private static void DisableIme(TextBox textBox)
        {
            if (textBox == null)
            {
                return;
            }

            InputMethod.SetIsInputMethodEnabled(textBox, false);
            InputMethod.SetPreferredImeState(textBox, InputMethodState.Off);
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
            var selected = _listBox != null ? _listBox.SelectedItem as ActivityCatalogItem : null;
            if (selected == null || selected.Type == null)
            {
                return;
            }

            bool ok = ActivityInsertService.TryAddActivity(selected.Type);
            if (ok)
            {
                Hide();
            }
        }
    }
}
