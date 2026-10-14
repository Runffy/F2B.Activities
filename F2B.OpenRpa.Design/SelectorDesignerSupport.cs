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
                ToolTip = "View or edit selector XML ({{variable}} keeps variables)"
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
                ToolTip = "View or edit selectors ({{variable}} keeps variables)"
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

            var displayItems = TryReadSelectorsForEditor(modelItem, propertyName);
            var hasVariables = displayItems.Any(SelectorExpressionBridge.ContainsVariablePlaceholder);

            var dialog = new SelectorsEditorDialog(displayItems, hasVariables)
            {
                Owner = owner
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (!TryWriteSelectorsFromEditor(modelItem, propertyName, dialog.Selectors?.ToArray() ?? Array.Empty<string>(), out var error))
            {
                MessageBox.Show(owner, error, "Selectors", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (editor != null)
            {
                editor.GetBindingExpression(ExpressionTextBox.ExpressionProperty)?.UpdateTarget();
            }
        }

        public static string[] TryReadSelectorsForEditor(ModelItem modelItem, string propertyName = "Selectors")
        {
            if (modelItem == null)
            {
                return Array.Empty<string>();
            }

            // Prefer expression text so variables are not collapsed; also covers managed VB arrays
            // (WF cannot store string[] as Literal, so "literal" selectors are New String() { "..." }).
            var expressionText = TryReadStringArrayExpressionText(modelItem, propertyName);
            if (!string.IsNullOrWhiteSpace(expressionText))
            {
                if (!LooksLikeVbStringArray(expressionText))
                {
                    return new[]
                    {
                        SelectorExpressionBridge.ExpressionToDisplay(expressionText, isLiteralValue: false)
                    };
                }

                var elements = SelectorExpressionBridge.ParseArrayElementExpressions(expressionText);
                return elements
                    .Select(element =>
                    {
                        if (TryParseQuotedLiteralElement(element, out var literal))
                        {
                            return SelectorExpressionBridge.ExpressionToDisplay(literal, isLiteralValue: true);
                        }

                        return SelectorExpressionBridge.ExpressionToDisplay(element, isLiteralValue: false);
                    })
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToArray();
            }

            var literals = TryReadComputedStringArray(modelItem, propertyName);
            if (literals != null && literals.Length > 0)
            {
                return SelectorExpressionBridge.ExpressionsToDisplay(literals, treatAsLiterals: true);
            }

            return Array.Empty<string>();
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

            // Legacy workflows store Selectors as VisualBasicValue("New String() { ... }").
            // Prefer editor bridge so variables become {{var}} rather than name strings.
            if (LooksLikeVbStringArray(expressionText))
            {
                return TryReadSelectorsForEditor(modelItem, propertyName);
            }

            return VbStringArrayExpression.Parse(expressionText);
        }

        public static bool TryWriteSelectorsFromEditor(
            ModelItem modelItem,
            string propertyName,
            string[] displayValues,
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

            var items = (displayValues ?? Array.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToArray();

            if (items.Length == 0)
            {
                error = "至少添加一个 selector。";
                return false;
            }

            // WF Literal<T> only allows value types and System.String — never string[].
            // Pure literal selectors are stored as VB: New String() { "..." }.
            // Items with {{var}} become VB expressions that keep variables as variables.
            string expression;
            if (SelectorExpressionBridge.TryBuildLiteralArray(items, out var literals))
            {
                expression = VbStringArrayExpression.Build(literals);
            }
            else
            {
                expression = SelectorExpressionBridge.BuildArrayExpression(items);
            }

            property.SetValue(new InArgument<string[]>(new VisualBasicValue<string[]>(expression)));
            return true;
        }

        /// <summary>
        /// Writes Selectors as a VB <c>New String() { ... }</c> expression (WF cannot Literal string[]).
        /// Items with <c>{{variable}}</c> keep variables; pure XML stays quoted string literals inside the array.
        /// </summary>
        public static bool TryWriteStringArrayLiteral(
            ModelItem modelItem,
            string propertyName,
            string[] values,
            out string error)
        {
            return TryWriteSelectorsFromEditor(modelItem, propertyName, values, out error);
        }

        [Obsolete("Use TryWriteStringArrayLiteral. Kept for binary compatibility.")]
        public static bool TryWriteStringArrayExpression(
            ModelItem modelItem,
            string propertyName,
            string[] values,
            out string error)
        {
            return TryWriteStringArrayLiteral(modelItem, propertyName, values, out error);
        }

        public static bool IsManagedSelectorsExpression(ModelItem modelItem, string propertyName)
        {
            return IsLiteralSelectors(modelItem, propertyName);
        }

        public static bool IsLiteralSelectors(ModelItem modelItem, string propertyName)
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
            if (value is Literal<string[]> || value is Literal<List<string>> || value is Literal<Array>)
            {
                return true;
            }

            // Legacy designer-written VB array literals with only quoted strings are treated as managed literals.
            if (value is VisualBasicValue<string[]> vb && !string.IsNullOrWhiteSpace(vb.ExpressionText))
            {
                var trimmed = vb.ExpressionText.TrimStart();
                if (!(trimmed.StartsWith("New String()", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("New String() {", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("New String(){", StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                var elements = SelectorExpressionBridge.ParseArrayElementExpressions(vb.ExpressionText);
                return elements.Length > 0 && elements.All(element => TryParseQuotedLiteralElement(element, out _));
            }

            return false;
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

            // Do not evaluate VisualBasicValue / other expressions via ComputedValue —
            // that would collapse variables into design-time strings or fail silently.
            if (value is ITextExpression)
            {
                return null;
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

            var displayText = TryReadSelectorForEditor(modelItem, propertyName) ?? string.Empty;
            var hasVariables = SelectorExpressionBridge.ContainsVariablePlaceholder(displayText);

            var dialog = new SelectorXmlEditorDialog(displayText, hasVariables)
            {
                Owner = owner
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (!TryWriteSelectorFromEditor(modelItem, propertyName, dialog.SelectorText ?? string.Empty, out var error))
            {
                MessageBox.Show(owner, error, "Selector", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (editor != null)
            {
                editor.GetBindingExpression(ExpressionTextBox.ExpressionProperty)?.UpdateTarget();
            }
        }

        public static string TryReadSelectorForEditor(ModelItem modelItem, string propertyName = "Selector")
        {
            if (modelItem == null)
            {
                return string.Empty;
            }

            if (IsLiteralSelector(modelItem, propertyName))
            {
                return SelectorExpressionBridge.ExpressionToDisplay(
                    TryReadLiteralSelectorValue(modelItem, propertyName) ?? string.Empty,
                    isLiteralValue: true);
            }

            var expressionText = TryReadSelectorExpressionText(modelItem, propertyName);
            if (string.IsNullOrWhiteSpace(expressionText))
            {
                return string.Empty;
            }

            return SelectorExpressionBridge.ExpressionToDisplay(expressionText, isLiteralValue: false);
        }

        public static string TryReadSelectorText(ModelItem modelItem, string propertyName = "Selector")
        {
            if (modelItem == null)
            {
                return null;
            }

            return TryReadSelectorForEditor(modelItem, propertyName);
        }

        public static bool TryWriteSelectorFromEditor(
            ModelItem modelItem,
            string propertyName,
            string displayText,
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

            if (!SelectorExpressionBridge.TryDisplayToExpression(
                    displayText,
                    out var expressionText,
                    out var isPureLiteral,
                    out var literalValue))
            {
                error = "Unable to parse selector editor text.";
                return false;
            }

            if (isPureLiteral)
            {
                property.SetValue(new InArgument<string>(new Literal<string>(literalValue ?? string.Empty)));
                return true;
            }

            property.SetValue(new InArgument<string>(new VisualBasicValue<string>(expressionText)));
            return true;
        }

        public static bool TryWriteSelectorLiteral(ModelItem modelItem, string propertyName, string text, out string error)
        {
            return TryWriteSelectorFromEditor(modelItem, propertyName, text, out error);
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

        private static string TryReadSelectorExpressionText(ModelItem modelItem, string propertyName)
        {
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

            if (value is Literal<string> literal)
            {
                return literal.Value;
            }

            return expressionProperty.Value.ToString();
        }

        private static string TryReadLiteralSelectorValue(ModelItem modelItem, string propertyName)
        {
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

            if (expressionProperty.Value.GetCurrentValue() is Literal<string> literal)
            {
                return literal.Value;
            }

            return null;
        }

        private static bool LooksLikeVbStringArray(string expressionText)
        {
            if (string.IsNullOrWhiteSpace(expressionText))
            {
                return false;
            }

            var trimmed = expressionText.TrimStart();
            return trimmed.StartsWith("New String()", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("New String() {", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("New String(){", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseQuotedLiteralElement(string element, out string literal)
        {
            literal = null;
            if (string.IsNullOrWhiteSpace(element))
            {
                return false;
            }

            // Reuse bridge by pretending it's a quoted expression via Vb helper path:
            // only accept pure quoted strings / vbCrLf concatenations of quotes.
            var parsed = VbStringArrayExpression.Parse("New String() { " + element + " }");
            if (parsed.Length != 1)
            {
                return false;
            }

            var trimmed = element.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
            {
                literal = parsed[0];
                return true;
            }

            if (trimmed.IndexOf('"') >= 0
                && trimmed.IndexOf("vbCrLf", StringComparison.OrdinalIgnoreCase) >= 0
                && trimmed.IndexOf("{{", StringComparison.Ordinal) < 0)
            {
                // Quoted multiline literal concatenation without variables.
                literal = parsed[0];
                return true;
            }

            return false;
        }
    }
}
