using System;
using System.Activities;
using System.Activities.Expressions;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualBasic.Activities;

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

        public static FrameworkElement CreateSelectorsRow(
            string label,
            ExpressionTextBox editor,
            string propertyName,
            Func<ModelItem> getModelItem,
            string labelSharedSizeGroup,
            double editorMinWidth,
            out Border editorBorder,
            double top = 0)
        {
            NormalizeSelectorsArrayEditor(editor, editorMinWidth);

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
                MinHeight = 44,
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
                ToolTip = "View or edit selectors list"
            };
            viewButton.Click += (sender, args) =>
            {
                var modelItem = getModelItem?.Invoke();
                if (modelItem == null)
                {
                    return;
                }

                OpenSelectorsEditor(modelItem, propertyName, editor, Window.GetWindow(row));
            };
            Grid.SetColumn(viewButton, 2);
            row.Children.Add(viewButton);

            return row;
        }

        public static bool HasSelectorsFilled(ModelItem modelItem, string propertyName = "Selectors")
        {
            var items = TryReadStringArray(modelItem, propertyName);
            if (items != null && items.Length > 0 && items.Any(item => !string.IsNullOrWhiteSpace(item)))
            {
                return true;
            }

            var expressionText = TryReadStringArrayExpressionText(modelItem, propertyName);
            return !string.IsNullOrWhiteSpace(expressionText);
        }

        public static void OpenSelectorsEditor(ModelItem modelItem, string propertyName, ExpressionTextBox editor, Window owner)
        {
            if (modelItem == null)
            {
                return;
            }

            var currentItems = TryReadStringArray(modelItem, propertyName) ?? Array.Empty<string>();
            var isExpression = !IsManagedSelectorsExpression(modelItem, propertyName) &&
                !string.IsNullOrWhiteSpace(TryReadStringArrayExpressionText(modelItem, propertyName));

            var dialog = new SelectorsEditorDialog(currentItems, isExpression)
            {
                Owner = owner
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (!TryWriteStringArrayExpression(modelItem, propertyName, dialog.Selectors?.ToArray() ?? Array.Empty<string>(), out var error))
            {
                MessageBox.Show(owner, error, "Selectors", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (editor != null)
            {
                editor.GetBindingExpression(ExpressionTextBox.ExpressionProperty)?.UpdateTarget();
            }
        }

        public static string[] TryReadStringArray(ModelItem modelItem, string propertyName = "Selectors")
        {
            if (modelItem == null)
            {
                return null;
            }

            var computed = TryReadComputedStringArray(modelItem, propertyName);
            if (computed != null)
            {
                return computed;
            }

            var expressionText = TryReadStringArrayExpressionText(modelItem, propertyName);
            if (string.IsNullOrWhiteSpace(expressionText))
            {
                return null;
            }

            return VbStringArrayExpression.Parse(expressionText);
        }

        public static bool TryWriteStringArrayExpression(
            ModelItem modelItem,
            string propertyName,
            string[] values,
            out string error)
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

            var expressionText = VbStringArrayExpression.Build(values ?? Array.Empty<string>());
            property.SetValue(new InArgument<string[]>(new VisualBasicValue<string[]>(expressionText)));
            return true;
        }

        public static bool IsManagedSelectorsExpression(ModelItem modelItem, string propertyName)
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

            var value = expressionProperty.Value.GetCurrentValue();
            return value is VisualBasicValue<string[]>
                || value is Literal<string[]>
                || value is Literal<List<string>>;
        }

        public static string TryReadStringArrayExpressionText(ModelItem modelItem, string propertyName = "Selectors")
        {
            if (modelItem == null)
            {
                return null;
            }

            var property = modelItem.Properties[propertyName];
            if (property?.Value == null)
            {
                return null;
            }

            var expressionProperty = property.Value.Properties["Expression"];
            if (expressionProperty?.Value == null)
            {
                return null;
            }

            var value = expressionProperty.Value.GetCurrentValue();
            if (value is ITextExpression textExpression && !string.IsNullOrWhiteSpace(textExpression.ExpressionText))
            {
                return textExpression.ExpressionText;
            }

            return expressionProperty.Value.ToString();
        }

        private static string[] TryReadComputedStringArray(ModelItem modelItem, string propertyName)
        {
            var property = modelItem?.Properties[propertyName];
            if (property?.Value == null)
            {
                return null;
            }

            var expressionProperty = property.Value.Properties["Expression"];
            if (expressionProperty == null)
            {
                return null;
            }

            var value = expressionProperty.Value?.GetCurrentValue();
            if (value is Literal<string[]> stringArrayLiteral)
            {
                return stringArrayLiteral.Value ?? Array.Empty<string>();
            }

            if (value is Literal<List<string>> listLiteral && listLiteral.Value != null)
            {
                return listLiteral.Value.ToArray();
            }

            if (value is Literal<Array> arrayLiteral && arrayLiteral.Value is string[] stringArray)
            {
                return stringArray;
            }

            if (expressionProperty.ComputedValue is string[] computedArray)
            {
                return computedArray;
            }

            if (expressionProperty.ComputedValue is IEnumerable<string> computedEnumerable)
            {
                return computedEnumerable.ToArray();
            }

            if (expressionProperty.ComputedValue is Array genericArray && genericArray.Length > 0)
            {
                var strings = new string[genericArray.Length];
                for (var i = 0; i < genericArray.Length; i++)
                {
                    strings[i] = genericArray.GetValue(i)?.ToString() ?? string.Empty;
                }

                return strings;
            }

            return null;
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

        public static void NormalizeSelectorsArrayEditor(ExpressionTextBox editor, double editorMinWidth)
        {
            if (editor == null)
            {
                return;
            }

            editor.VerticalAlignment = VerticalAlignment.Top;
            editor.HorizontalAlignment = HorizontalAlignment.Left;
            editor.MinWidth = editorMinWidth;
            editor.Width = editorMinWidth;
            editor.MinHeight = 44;
            editor.Height = 44;
            editor.MaxHeight = 120;
            editor.MinLines = 2;
            editor.MaxLines = 6;
            editor.FontSize = 12;
            editor.FontFamily = new FontFamily("Consolas");
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
