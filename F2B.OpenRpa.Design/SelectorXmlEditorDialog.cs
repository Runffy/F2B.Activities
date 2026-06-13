using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace F2B.OpenRpa.Design
{
    public sealed class SelectorXmlEditorDialog : Window
    {
        private readonly TextBox _textBox;
        private readonly TextBlock _hintText;

        public string SelectorText { get; private set; }

        public SelectorXmlEditorDialog(string initialText, bool replaceExpressionWarning)
        {
            Title = "Selector XML";
            Width = 560;
            Height = 400;
            MinWidth = 420;
            MinHeight = 280;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResizeWithGrip;

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _hintText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 0, 0, 8),
                Visibility = replaceExpressionWarning ? Visibility.Visible : Visibility.Collapsed
            };
            if (replaceExpressionWarning)
            {
                _hintText.Text = "当前 Selector 是表达式。在此保存将替换为字面量 XML。";
            }

            Grid.SetRow(_hintText, 0);
            root.Children.Add(_hintText);

            _textBox = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Text = initialText ?? string.Empty
            };
            Grid.SetRow(_textBox, 1);
            root.Children.Add(_textBox);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var okButton = new Button
            {
                Content = "OK",
                MinWidth = 72,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            okButton.Click += (sender, args) =>
            {
                SelectorText = _textBox.Text ?? string.Empty;
                DialogResult = true;
                Close();
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 72,
                IsCancel = true
            };
            cancelButton.Click += (sender, args) =>
            {
                DialogResult = false;
                Close();
            };

            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            Content = root;
            Loaded += (sender, args) => _textBox.Focus();
        }
    }
}
