using System;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace F2B.Basic
{
    /// <summary>
    /// Shared canvas expression editor helpers for Basic activity designers.
    /// </summary>
    internal static class BasicDesignerShared
    {
        internal static FrameworkElement CreateLabeledExpressionEditor(
            string label,
            string bindingPath,
            Type expressionType,
            string hint,
            out Border editorBorder,
            out ExpressionTextBox expressionTextBox,
            double editorWidth = 200)
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = label,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            });

            expressionTextBox = new ExpressionTextBox
            {
                Width = editorWidth,
                MaxWidth = editorWidth,
                HintText = hint,
                ExpressionType = expressionType,
                MinLines = 1,
                MaxLines = 1
            };

            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.ExpressionProperty, new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay,
                Converter = new ArgumentToExpressionConverter(),
                ConverterParameter = "In"
            });

            editorBorder = new Border
            {
                Margin = new Thickness(4, 0, 0, 0),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = expressionTextBox
            };

            row.Children.Add(editorBorder);
            return row;
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

        internal static bool HasEditorInput(ExpressionTextBox editor)
        {
            return editor != null && editor.Expression != null;
        }

        internal static void SetRequiredBorder(Border border, bool filled)
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
