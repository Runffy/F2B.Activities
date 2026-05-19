using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace F2B.Basic
{
    /// <summary>
    /// 画布上<strong>仅</strong>显示 URL；Method、Headers、Params、输出 Response 等在属性网格中编辑。
    /// </summary>
    public sealed class HttpRequestDesigner : ActivityDesigner
    {
        private const int LabelWidth = 96;

        private Border _urlBorder;
        private ExpressionTextBox _urlBox;

        public HttpRequestDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
            };

            var panel = new StackPanel();
            panel.Children.Add(
                CreateExpressionRow(
                    "URL",
                    "ModelItem.Url",
                    typeof(string),
                    "https://...",
                    out _urlBorder,
                    out _urlBox,
                    "In",
                    1,
                    1));

            border.Child = panel;
            Content = border;
            Loaded += OnLoaded;
        }

        private static FrameworkElement CreateExpressionRow(
            string label,
            string bindingPath,
            Type expressionType,
            string hint,
            out Border editorBorder,
            out ExpressionTextBox expressionBox,
            string converterParameter,
            int minLines,
            int maxLines)
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(
                new TextBlock
                {
                    Text = label,
                    Width = LabelWidth,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                });

            expressionBox = new ExpressionTextBox
            {
                Margin = new Thickness(4, 0, 0, 0),
                Width = 200,
                MaxWidth = 200,
                HintText = hint,
                ExpressionType = expressionType,
                MinLines = minLines,
                MaxLines = maxLines,
            };

            BindingOperations.SetBinding(expressionBox, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(
                expressionBox,
                ExpressionTextBox.ExpressionProperty,
                new Binding(bindingPath)
                {
                    Mode = BindingMode.TwoWay,
                    Converter = new ArgumentToExpressionConverter(),
                    ConverterParameter = converterParameter,
                });

            editorBorder = new Border
            {
                Margin = new Thickness(0),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = expressionBox,
            };

            row.Children.Add(editorBorder);
            return row;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
        }

        private void RefreshRequiredBorders()
        {
            SetRequiredBorder(_urlBorder, IsArgumentFilled(ModelItem, "Url", _urlBox));
        }

        private static bool IsArgumentFilled(ModelItem modelItem, string propertyName, ExpressionTextBox editor)
        {
            var property = modelItem?.Properties[propertyName];
            if (property == null)
            {
                return HasEditorInput(editor);
            }

            if (property.IsSet)
            {
                return true;
            }

            if (property.Value == null)
            {
                return HasEditorInput(editor);
            }

            var expressionProperty = property.Value.Properties["Expression"];
            if (expressionProperty == null)
            {
                return HasEditorInput(editor);
            }

            if (expressionProperty.Value == null && expressionProperty.ComputedValue == null)
            {
                return HasEditorInput(editor);
            }

            if (expressionProperty.ComputedValue is string text)
            {
                return !string.IsNullOrWhiteSpace(text);
            }

            if (expressionProperty.Value != null)
            {
                string expressionText = expressionProperty.Value.ToString();
                return !string.IsNullOrWhiteSpace(expressionText);
            }

            return HasEditorInput(editor);
        }

        private static bool HasEditorInput(ExpressionTextBox editor)
        {
            return editor != null && editor.Expression != null;
        }

        private static void SetRequiredBorder(Border border, bool filled)
        {
            if (border == null)
            {
                return;
            }

            if (filled)
            {
                border.BorderBrush = Brushes.Transparent;
                border.BorderThickness = new Thickness(0);
                return;
            }

            border.BorderBrush = Brushes.Red;
            border.BorderThickness = new Thickness(1);
        }
    }
}
