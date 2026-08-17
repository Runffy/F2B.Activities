using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Ctrl+T dialog: paste full Exception.Source, Enter to search, Ctrl+Enter for newline, Esc to close.
    /// </summary>
    internal sealed class FaultPathSearchWindow : Window
    {
        private readonly TextBox _text;

        public FaultPathSearchWindow()
        {
            Title = "Go to Fault Path";
            Width = 720;
            Height = 320;
            MinWidth = 480;
            MinHeight = 220;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Background = Brushes.White;

            try
            {
                if (PluginContext.MainWindow != null)
                {
                    Owner = PluginContext.MainWindow;
                }
            }
            catch
            {
            }

            var root = new DockPanel { Margin = new Thickness(10) };
            var hint = new TextBlock
            {
                Text = "Paste full Exception.Source. Enter = search, Ctrl+Enter = newline, Esc = close.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = Brushes.DimGray
            };
            DockPanel.SetDock(hint, Dock.Top);
            root.Children.Add(hint);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };
            DockPanel.SetDock(buttons, Dock.Bottom);

            var go = new Button
            {
                Content = "Go",
                Width = 88,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            go.Click += (s, e) => RunSearch();
            buttons.Children.Add(go);

            var close = new Button
            {
                Content = "Close",
                Width = 88,
                Height = 28,
                IsCancel = true
            };
            close.Click += (s, e) => Close();
            buttons.Children.Add(close);
            root.Children.Add(buttons);

            _text = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                MinHeight = 140
            };
            _text.PreviewKeyDown += OnTextPreviewKeyDown;
            root.Children.Add(_text);

            Content = root;
            Loaded += (s, e) =>
            {
                _text.Focus();
                Keyboard.Focus(_text);
            };
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    Close();
                }
            };
        }

        private void OnTextPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Return && e.Key != Key.Enter)
            {
                return;
            }

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (ctrl)
            {
                // Insert newline manually because we may mark Enter as search.
                int caret = _text.CaretIndex;
                _text.Text = _text.Text.Insert(caret, Environment.NewLine);
                _text.CaretIndex = caret + Environment.NewLine.Length;
                e.Handled = true;
                return;
            }

            e.Handled = true;
            RunSearch();
        }

        private void RunSearch()
        {
            string source = _text.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                MessageBox.Show(this, "Paste an Exception.Source path first.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                bool ok = FaultPathNavigator.Navigate(source);
                if (ok)
                {
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
