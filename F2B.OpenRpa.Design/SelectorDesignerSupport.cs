using System;
using System.Activities;
using System.Activities.Expressions;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace F2B.OpenRpa.Design
{
    public static class SelectorDesignerSupport
    {
        private const double SelectorButtonWidth = 26;

        public static bool IsSingleSelectorProperty(string propertyName)
        {
            return string.Equals(propertyName, "Selector", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "ChildSelector", StringComparison.OrdinalIgnoreCase);
        }

        public static FrameworkElement CreateSelectorRow(
            string label,
            ExpressionTextBox editor,
            string propertyName,
            Func<ModelItem> getModelItem,
            string labelSharedSizeGroup,
            double editorMinWidth,
            out Border editorBorder,
            double top = 0)
        {
            NormalizeSelectorEditor(editor, editorMinWidth);

            var row = new Grid
            {
                Margin = new Thickness(0, top, 0, 0)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto,
                MinWidth = 78,
                SharedSizeGroup = labelSharedSizeGroup
            });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelTextBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                ToolTip = label
            };
            Grid.SetColumn(labelTextBlock, 0);
            row.Children.Add(labelTextBlock);

            var host = new Border
            {
                Margin = new Thickness(4, 0, 0, 0),
                MinWidth = editorMinWidth,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Left,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = editor
            };
            editorBorder = host;
            Grid.SetColumn(host, 1);
            row.Children.Add(host);

            var viewButton = new Button
            {
                Content = "...",
                Width = SelectorButtonWidth,
                Height = 22,
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "View or edit selector XML"
            };
            viewButton.Click += (sender, args) =>
            {
                var modelItem = getModelItem?.Invoke();
                if (modelItem == null)
                {
                    return;
                }

                OpenSelectorEditor(modelItem, propertyName, editor, Window.GetWindow(row));
            };
            Grid.SetColumn(viewButton, 2);
            row.Children.Add(viewButton);

            return row;
        }

        public static void OpenSelectorEditor(ModelItem modelItem, string propertyName, ExpressionTextBox editor, Window owner)
        {
            if (modelItem == null)
            {
                return;
            }

            var currentText = TryReadSelectorText(modelItem, propertyName) ?? string.Empty;
            var isExpression = !IsLiteralSelector(modelItem, propertyName) && !string.IsNullOrWhiteSpace(currentText);

            var dialog = new SelectorXmlEditorDialog(currentText, isExpression)
            {
                Owner = owner
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (!TryWriteSelectorLiteral(modelItem, propertyName, dialog.SelectorText ?? string.Empty, out var error))
            {
                MessageBox.Show(owner, error, "Selector", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (editor != null)
            {
                editor.GetBindingExpression(ExpressionTextBox.ExpressionProperty)?.UpdateTarget();
            }
        }

        public static string TryReadSelectorText(ModelItem modelItem, string propertyName = "Selector")
        {
            if (modelItem == null)
            {
                return null;
            }

            return TryReadInArgumentString(modelItem, propertyName);
        }

        public static bool TryWriteSelectorLiteral(ModelItem modelItem, string propertyName, string text, out string error)
        {
            error = null;
            if (modelItem == null)
            {
                error = "Activity model is not available.";
                return false;
            }

            var property = modelItem.Properties[propertyName];
            if (property == null)
            {
                error = "Property '" + propertyName + "' was not found.";
                return false;
            }

            property.SetValue(new InArgument<string>(new Literal<string>(text ?? string.Empty)));
            return true;
        }

        public static bool IsLiteralSelector(ModelItem modelItem, string propertyName)
        {
            if (modelItem == null)
            {
                return true;
            }

            var property = modelItem.Properties[propertyName];
            if (property?.Value == null)
            {
                return true;
            }

            var expressionProperty = property.Value.Properties["Expression"];
            if (expressionProperty?.Value == null)
            {
                return true;
            }

            return expressionProperty.Value.GetCurrentValue() is Literal<string>;
        }

        public static void NormalizeSelectorEditor(ExpressionTextBox editor, double editorMinWidth)
        {
            if (editor == null)
            {
                return;
            }

            editor.VerticalAlignment = VerticalAlignment.Center;
            editor.HorizontalAlignment = HorizontalAlignment.Left;
            editor.MinWidth = editorMinWidth;
            editor.Width = editorMinWidth;
            editor.MinHeight = 22;
            editor.Height = 22;
            editor.MaxHeight = 22;
            editor.MinLines = 1;
            editor.MaxLines = 1;
            editor.FontSize = 12;
        }

        private static string TryReadInArgumentString(ModelItem modelItem, string propertyName)
        {
            var property = modelItem.Properties[propertyName];
            if (property?.Value == null)
            {
                return null;
            }

            var expressionProperty = property.Value.Properties["Expression"];
            if (expressionProperty == null)
            {
                return null;
            }

            if (expressionProperty.ComputedValue is string computed)
            {
                return computed;
            }

            if (expressionProperty.Value != null)
            {
                var value = expressionProperty.Value.GetCurrentValue();
                if (value is Literal<string> literal)
                {
                    return literal.Value;
                }

                if (value is string literalString)
                {
                    return literalString;
                }
            }

            var text = expressionProperty.Value?.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : UnquoteLiteral(text);
        }

        private static string UnquoteLiteral(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            text = text.Trim();
            if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
            {
                return text
                    .Substring(1, text.Length - 2)
                    .Replace("\\r\\n", "\r\n")
                    .Replace("\\n", "\n")
                    .Replace("\\t", "\t")
                    .Replace("\\\"", "\"");
            }

            return text;
        }
    }
}
