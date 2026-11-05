using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using F2B.OpenRpa.Design;

namespace F2B.Basic
{
    public sealed class CommentDesigner : ActivityDesigner
    {
        private const int MaxVisibleLines = 5;
        private const double HeightSlack = 6;

        private readonly TextBox _commentBox;
        private bool _syncing;
        private bool _adjustingHeight;

        public CommentDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                MinWidth = 240
            };

            _commentBox = new TextBox
            {
                MinWidth = 220,
                MaxWidth = 420,
                AcceptsReturn = true,
                AcceptsTab = false,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.LightGray,
                Background = Brushes.White,
                FontSize = 13,
                Padding = new Thickness(4, 3, 4, 3),
                VerticalContentAlignment = VerticalAlignment.Top
            };
            _commentBox.TextChanged += OnCommentTextChanged;
            _commentBox.SizeChanged += OnCommentBoxSizeChanged;

            border.Child = _commentBox;
            ActivityDesignerCollapseHelper.Attach(this, border);
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            SyncFromModel();
            Dispatcher.BeginInvoke(new Action(AdjustHeight), DispatcherPriority.Loaded);
        }

        private void OnCommentBoxSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_adjustingHeight)
            {
                return;
            }

            if (Math.Abs(e.PreviousSize.Width - e.NewSize.Width) > 0.5)
            {
                AdjustHeight();
            }
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName)
                || string.Equals(e.PropertyName, "CommentText", StringComparison.Ordinal)
                || string.Equals(e.PropertyName, "Color", StringComparison.Ordinal)
                || string.Equals(e.PropertyName, "OtherColor", StringComparison.Ordinal))
            {
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        SyncFromModel();
                        AdjustHeight();
                    }),
                    DispatcherPriority.Background);
            }
        }

        private void OnCommentTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing || ModelItem == null)
            {
                return;
            }

            string current = ReadCommentText(ModelItem);
            if (!string.Equals(current, _commentBox.Text, StringComparison.Ordinal))
            {
                ModelItem.Properties["CommentText"].SetValue(_commentBox.Text ?? string.Empty);
            }

            AdjustHeight();
        }

        private void SyncFromModel()
        {
            if (ModelItem == null)
            {
                return;
            }

            _syncing = true;
            try
            {
                string text = ReadCommentText(ModelItem);
                if (!string.Equals(_commentBox.Text, text, StringComparison.Ordinal))
                {
                    int caret = _commentBox.CaretIndex;
                    _commentBox.Text = text;
                    _commentBox.CaretIndex = Math.Max(0, Math.Min(caret, _commentBox.Text.Length));
                }

                CommentColor color = ReadColor(ModelItem);
                string other = ReadOtherColor(ModelItem);
                _commentBox.Foreground = CommentActivity.ResolveForeground(color, other);
            }
            finally
            {
                _syncing = false;
            }
        }

        private void AdjustHeight()
        {
            if (_adjustingHeight)
            {
                return;
            }

            _adjustingHeight = true;
            try
            {
                double width = _commentBox.ActualWidth;
                if (width <= 0)
                {
                    width = _commentBox.MinWidth;
                }

                double oneLineHeight = MeasureTextHeight("字", width);
                double fiveLineHeight = MeasureTextHeight("字\n字\n字\n字\n字", width);
                double contentHeight = MeasureTextHeight(
                    string.IsNullOrEmpty(_commentBox.Text) ? " " : _commentBox.Text,
                    width);

                if (oneLineHeight < 8)
                {
                    oneLineHeight = (_commentBox.FontSize * 1.5) + GetVerticalChrome() + HeightSlack;
                }

                if (fiveLineHeight < oneLineHeight * 2)
                {
                    fiveLineHeight = oneLineHeight * MaxVisibleLines;
                }

                // FormattedText vs TextBox template mismatch: pad one line so last line isn't clipped.
                double lineBuffer = Math.Max(8, oneLineHeight - GetVerticalChrome() - HeightSlack);
                contentHeight += lineBuffer;
                fiveLineHeight += lineBuffer;

                double minHeight = oneLineHeight;
                double maxHeight = fiveLineHeight;

                if (contentHeight <= maxHeight + 0.5)
                {
                    _commentBox.MaxHeight = double.PositiveInfinity;
                    _commentBox.Height = Math.Max(minHeight, contentHeight);
                    _commentBox.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                }
                else
                {
                    _commentBox.Height = maxHeight;
                    _commentBox.MaxHeight = maxHeight;
                    _commentBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                }
            }
            finally
            {
                _adjustingHeight = false;
            }
        }

        private double MeasureTextHeight(string text, double totalWidth)
        {
            double contentWidth = Math.Max(1, totalWidth - GetHorizontalChrome());
            double pixelsPerDip = 1.0;
            if (_commentBox.IsLoaded)
            {
                try
                {
                    pixelsPerDip = VisualTreeHelper.GetDpi(_commentBox).PixelsPerDip;
                }
                catch
                {
                    // fall back to 1.0
                }
            }

            var formatted = new FormattedText(
                string.IsNullOrEmpty(text) ? " " : text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    _commentBox.FontFamily,
                    _commentBox.FontStyle,
                    _commentBox.FontWeight,
                    _commentBox.FontStretch),
                _commentBox.FontSize,
                Brushes.Black,
                pixelsPerDip)
            {
                MaxTextWidth = contentWidth,
                Trimming = TextTrimming.None
            };

            double textHeight = formatted.Height;
            if (textHeight < 1)
            {
                textHeight = _commentBox.FontSize * 1.35;
            }

            return textHeight + GetVerticalChrome() + HeightSlack;
        }

        private double GetHorizontalChrome()
        {
            return _commentBox.Padding.Left
                   + _commentBox.Padding.Right
                   + _commentBox.BorderThickness.Left
                   + _commentBox.BorderThickness.Right;
        }

        private double GetVerticalChrome()
        {
            return _commentBox.Padding.Top
                   + _commentBox.Padding.Bottom
                   + _commentBox.BorderThickness.Top
                   + _commentBox.BorderThickness.Bottom;
        }

        private static string ReadCommentText(ModelItem modelItem)
        {
            object value = modelItem?.Properties["CommentText"]?.ComputedValue;
            return value as string ?? string.Empty;
        }

        private static CommentColor ReadColor(ModelItem modelItem)
        {
            object value = modelItem?.Properties["Color"]?.ComputedValue;
            if (value is CommentColor color)
            {
                return color;
            }

            return CommentColor.Green;
        }

        private static string ReadOtherColor(ModelItem modelItem)
        {
            object value = modelItem?.Properties["OtherColor"]?.ComputedValue;
            return value as string ?? string.Empty;
        }
    }
}
