using System;
using System.Activities;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace F2B.Microsoft.Word
{
    internal static class WordDesignerShared
    {
        internal const double RowLabelMinWidth = 96;
        internal const double EditorMinWidth = 190;
        internal const double RowSpacing = 4;

        internal static FrameworkElement CreateRow(string label, FrameworkElement editor, string sharedSizeGroup, double top = 0)
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
            return CreateExpressionTextBox(pathToArgument, expressionType, "In");
        }

        internal static ExpressionTextBox CreateOutExpressionTextBox(string pathToArgument, Type expressionType)
        {
            return CreateExpressionTextBox(pathToArgument, expressionType, "Out");
        }

        internal static ExpressionTextBox CreateInOutExpressionTextBox(string pathToArgument, Type expressionType)
        {
            return CreateExpressionTextBox(pathToArgument, expressionType, "InOut");
        }

        private static ExpressionTextBox CreateExpressionTextBox(string pathToArgument, Type expressionType, string direction)
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
                ConverterParameter = direction
            });

            return editor;
        }

        internal static ComboBox BuildDescriptionComboBox<TEnum>() where TEnum : struct
        {
            var comboBox = new ComboBox
            {
                IsEditable = false,
                DisplayMemberPath = "Display",
                SelectedValuePath = "Value",
                MinWidth = EditorMinWidth,
                Width = EditorMinWidth
            };

            foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
            {
                comboBox.Items.Add(new EnumDisplayItem
                {
                    Value = value,
                    Display = GetEnumDescription(value)
                });
            }

            return comboBox;
        }

        internal static string GetEnumDescription<TEnum>(TEnum value) where TEnum : struct
        {
            var name = value.ToString();
            var field = typeof(TEnum).GetField(name);
            var description = field?.GetCustomAttribute<DescriptionAttribute>();
            return description != null && !string.IsNullOrWhiteSpace(description.Description)
                ? description.Description
                : name;
        }

        internal static void SelectEnumItem(ComboBox comboBox, object value)
        {
            foreach (EnumDisplayItem item in comboBox.Items)
            {
                if (Equals(item.Value, value))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        internal static TEnum ReadSelectedEnum<TEnum>(ComboBox comboBox, TEnum fallback) where TEnum : struct
        {
            if (comboBox.SelectedItem is EnumDisplayItem item && item.Value is TEnum value)
            {
                return value;
            }

            return fallback;
        }

        internal static TEnum ReadEnum<TEnum>(ModelItem modelItem, string propertyName, TEnum fallback) where TEnum : struct
        {
            if (modelItem?.Properties[propertyName]?.ComputedValue is TEnum value)
            {
                return value;
            }

            return fallback;
        }

        internal static bool IsArgumentFilled(ModelItem modelItem, string propertyName, ExpressionTextBox editor)
        {
            var property = modelItem?.Properties[propertyName];
            if (property == null)
            {
                return editor != null && editor.Expression != null;
            }

            if (property.IsSet || property.ComputedValue != null)
            {
                return true;
            }

            if (property.Value == null)
            {
                return editor != null && editor.Expression != null;
            }

            var propertyValueText = property.Value.ToString();
            if (!string.IsNullOrWhiteSpace(propertyValueText) &&
                !string.Equals(propertyValueText, "null", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var expressionProperty = property.Value.Properties["Expression"];
            if (expressionProperty?.ComputedValue is string text)
            {
                return !string.IsNullOrWhiteSpace(text);
            }

            if (expressionProperty?.Value != null)
            {
                return !string.IsNullOrWhiteSpace(expressionProperty.Value.ToString());
            }

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

        internal sealed class EnumDisplayItem
        {
            public object Value { get; set; }

            public string Display { get; set; }
        }
    }
}
