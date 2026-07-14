using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using F2B.Browser.Chromium.Cdp.Selectors;
using F2B.OpenRpa.Design;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    internal static class CdpDesignerShared
    {
        internal const double RowLabelMinWidth = 78;
        internal const double EditorMinWidth = 190;
        internal const double RowSpacing = 4;

        internal static FrameworkElement CreateRow(
            string label,
            FrameworkElement editor,
            string sharedSizeGroup,
            double top = 0)
        {
            return CreateRow(label, editor, sharedSizeGroup, out _, top);
        }

        internal static FrameworkElement CreateRow(
            string label,
            FrameworkElement editor,
            string sharedSizeGroup,
            out Border editorBorder,
            double top = 0)
        {
            if (editor is ExpressionTextBox expressionTextBox)
            {
                SelectorDesignerSupport.NormalizeSelectorEditor(expressionTextBox, EditorMinWidth);
            }

            NormalizeEditor(editor);

            var row = new Grid { Margin = new Thickness(0, top, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto,
                MinWidth = RowLabelMinWidth,
                SharedSizeGroup = sharedSizeGroup
            });
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
                MinWidth = EditorMinWidth,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Left,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = editor
            };
            editorBorder = host;
            Grid.SetColumn(host, 1);
            row.Children.Add(host);
            return row;
        }

        internal static void NormalizeEditor(FrameworkElement editor)
        {
            editor.VerticalAlignment = VerticalAlignment.Center;
            editor.HorizontalAlignment = HorizontalAlignment.Left;

            if (editor is Control control)
            {
                control.FontSize = 12;
                control.MinHeight = 22;
                control.Height = 22;
            }

            if (editor is ComboBox comboBox)
            {
                comboBox.MinWidth = EditorMinWidth;
                comboBox.Width = EditorMinWidth;
            }

            if (editor is ExpressionTextBox expressionTextBox)
            {
                expressionTextBox.MinWidth = EditorMinWidth;
                expressionTextBox.Width = EditorMinWidth;
                expressionTextBox.MinHeight = 22;
                expressionTextBox.Height = 22;
                expressionTextBox.MaxHeight = 22;
            }
        }

        internal static ExpressionTextBox CreateInExpressionTextBox(string pathToArgument, Type expressionType)
        {
            var editor = new ExpressionTextBox
            {
                PathToArgument = pathToArgument,
                ExpressionType = expressionType,
                MinLines = 1,
                MaxLines = 1
            };

            BindingOperations.SetBinding(editor, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(editor, ExpressionTextBox.ExpressionProperty, new Binding("ModelItem." + pathToArgument)
            {
                Mode = BindingMode.TwoWay,
                Converter = new ArgumentToExpressionConverter(),
                ConverterParameter = "In"
            });

            return editor;
        }

        internal static ExpressionTextBox CreateOutExpressionTextBox(string pathToArgument, Type expressionType)
        {
            var editor = new ExpressionTextBox
            {
                PathToArgument = pathToArgument,
                ExpressionType = expressionType,
                MinLines = 1,
                MaxLines = 1
            };

            BindingOperations.SetBinding(editor, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(editor, ExpressionTextBox.ExpressionProperty, new Binding("ModelItem." + pathToArgument)
            {
                Mode = BindingMode.TwoWay,
                Converter = new ArgumentToExpressionConverter(),
                ConverterParameter = "Out"
            });

            return editor;
        }

        internal static ExpressionTextBox CreateInOutExpressionTextBox(string pathToArgument, Type expressionType)
        {
            var editor = new ExpressionTextBox
            {
                PathToArgument = pathToArgument,
                ExpressionType = expressionType,
                MinLines = 1,
                MaxLines = 1
            };

            BindingOperations.SetBinding(editor, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(editor, ExpressionTextBox.ExpressionProperty, new Binding("ModelItem." + pathToArgument)
            {
                Mode = BindingMode.TwoWay,
                Converter = new ArgumentToExpressionConverter(),
                ConverterParameter = "InOut"
            });

            return editor;
        }

        internal static ComboBox BuildEnumComboBox<TEnum>() where TEnum : struct
        {
            var comboBox = new ComboBox { IsEditable = false };
            foreach (var value in Enum.GetValues(typeof(TEnum)))
            {
                comboBox.Items.Add(value);
            }

            return comboBox;
        }

        internal static void BindExpressionOwner(DependencyObject parent, ModelItem owner)
        {
            if (parent is ExpressionTextBox expressionTextBox)
            {
                expressionTextBox.OwnerActivity = owner;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childCount; i++)
            {
                BindExpressionOwner(VisualTreeHelper.GetChild(parent, i), owner);
            }
        }

        internal static bool IsArgumentFilled(ModelItem modelItem, string propertyName, ExpressionTextBox editor)
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

            if (property.ComputedValue != null)
            {
                return true;
            }

            if (property.Value == null)
            {
                return HasEditorInput(editor);
            }

            var propertyValueText = property.Value.ToString();
            if (!string.IsNullOrWhiteSpace(propertyValueText) &&
                !string.Equals(propertyValueText, "null", StringComparison.OrdinalIgnoreCase))
            {
                return true;
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
                var expressionText = expressionProperty.Value.ToString();
                return !string.IsNullOrWhiteSpace(expressionText);
            }

            return HasEditorInput(editor);
        }

        internal static bool HasEditorInput(ExpressionTextBox editor)
        {
            return editor != null && editor.Expression != null;
        }

        internal static void SetRequiredBorder(Border border, bool required, bool filled)
        {
            if (border == null)
            {
                return;
            }

            if (!required || filled)
            {
                border.BorderBrush = Brushes.Transparent;
                border.BorderThickness = new Thickness(0);
                return;
            }

            border.BorderBrush = Brushes.Red;
            border.BorderThickness = new Thickness(1);
        }

        internal static bool SelectorHasWnd(ModelItem modelItem, string propertyName = "Selector")
        {
            var resolvedOrLiteral = TryReadSelectorText(modelItem, propertyName);
            if (SelectorXmlSerializer.HasWndLevel(resolvedOrLiteral))
            {
                return true;
            }

            // Design-time: VB expressions like `wndSelector & vbCrLf & "<ctrl…/>"` are not
            // deserializable as selector XML, but clearly resolve to a window-rooted selector.
            var expressionText = TryReadArgumentExpressionText(modelItem, propertyName);
            if (string.IsNullOrWhiteSpace(expressionText))
            {
                expressionText = resolvedOrLiteral;
            }

            if (string.IsNullOrWhiteSpace(expressionText))
            {
                return false;
            }

            if (expressionText.IndexOf("<wnd", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // Common workflow variable holding the full <wnd …/> fragment.
            if (expressionText.IndexOf("wndSelector", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        internal static string TryReadSelectorText(ModelItem modelItem, string propertyName = "Selector")
        {
            return SelectorDesignerSupport.TryReadSelectorText(modelItem, propertyName);
        }

        /// <summary>
        /// Best-effort read of the argument's design-time expression text (not evaluated value).
        /// </summary>
        internal static string TryReadArgumentExpressionText(ModelItem modelItem, string propertyName)
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

            try
            {
                var expression = expressionProperty.Value.GetCurrentValue();
                if (expression != null)
                {
                    var expressionTextProperty = expression.GetType().GetProperty("ExpressionText");
                    if (expressionTextProperty?.GetValue(expression) is string expressionText &&
                        !string.IsNullOrWhiteSpace(expressionText))
                    {
                        return expressionText;
                    }
                }
            }
            catch
            {
                // Ignore reflection failures; fall back to ToString().
            }

            var text = expressionProperty.Value.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        internal static void SyncCombo<T>(ComboBox comboBox, T value, ref bool syncingFlag)
        {
            syncingFlag = true;
            comboBox.SelectedItem = value;
            syncingFlag = false;
        }

        internal static void ClearArgumentIfHidden(ModelItem modelItem, string propertyName, bool shouldKeep)
        {
            if (shouldKeep || modelItem == null)
            {
                return;
            }

            var property = modelItem.Properties[propertyName];
            if (property == null || !property.IsSet)
            {
                return;
            }

            property.SetValue(null);
        }
    }
}
