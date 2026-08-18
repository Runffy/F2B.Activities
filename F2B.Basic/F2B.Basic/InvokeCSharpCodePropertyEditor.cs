using System;
using System.Activities;
using System.Activities.Expressions;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.PropertyEditing;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.VisualBasic.Activities;
using OpenRPA.Interfaces;

namespace F2B.Basic
{
    /// <summary>
    /// Property-grid editor: single-line text box plus "..." that opens the same Code window as the canvas button.
    /// </summary>
    public sealed class InvokeCSharpCodePropertyEditor : DialogPropertyValueEditor
    {
        public InvokeCSharpCodePropertyEditor()
        {
            var template = new DataTemplate();
            FrameworkElementFactory row = new FrameworkElementFactory(typeof(DockPanel));
            row.SetValue(DockPanel.LastChildFillProperty, true);

            FrameworkElementFactory ellipsis = new FrameworkElementFactory(typeof(EditModeSwitchButton));
            ellipsis.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 0, 0, 0));
            ellipsis.SetValue(EditModeSwitchButton.TargetEditModeProperty, PropertyContainerEditMode.Dialog);
            ellipsis.SetValue(DockPanel.DockProperty, Dock.Right);
            row.AppendChild(ellipsis);

            FrameworkElementFactory box = new FrameworkElementFactory(typeof(TextBox));
            box.SetValue(TextBox.AcceptsReturnProperty, false);
            box.SetValue(TextBox.TextWrappingProperty, TextWrapping.NoWrap);
            box.SetValue(TextBox.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            box.SetValue(TextBox.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            box.SetValue(TextBox.BorderThicknessProperty, new Thickness(0));
            box.SetValue(TextBox.PaddingProperty, new Thickness(2, 1, 2, 1));
            box.SetValue(FrameworkElement.MinHeightProperty, 20.0);
            box.SetValue(FrameworkElement.MaxHeightProperty, 22.0);
            box.SetValue(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center);
            box.SetBinding(TextBox.TextProperty, new Binding("Value")
            {
                Mode = BindingMode.TwoWay,
                Converter = new CodeArgumentConverter()
            });
            row.AppendChild(box);

            template.VisualTree = row;
            InlineEditorTemplate = template;
        }

        public override void ShowDialog(PropertyValue propertyValue, IInputElement commandSource)
        {
            if (propertyValue == null || propertyValue.ParentProperty == null)
            {
                return;
            }

            var activity = new ModelPropertyEntryToOwnerActivityConverter().Convert(
                propertyValue.ParentProperty,
                typeof(ModelItem),
                false,
                null) as ModelItem;
            if (activity == null)
            {
                return;
            }

            Window owner = GenericTools.MainWindow;
            var source = commandSource as DependencyObject;
            if (source != null)
            {
                Window fromSource = Window.GetWindow(source);
                if (fromSource != null)
                {
                    owner = fromSource;
                }
            }

            InvokeCSharpCodeEditor.Show(owner, activity);
        }

        private sealed class CodeArgumentConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return Unwrap(value);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                string text = value as string ?? string.Empty;
                return new InArgument<string>
                {
                    Expression = new Literal<string>(text)
                };
            }

            private static string Unwrap(object value)
            {
                var argument = value as InArgument<string>;
                if (argument == null)
                {
                    return value as string ?? string.Empty;
                }

                var literal = argument.Expression as Literal<string>;
                if (literal != null)
                {
                    return literal.Value ?? string.Empty;
                }

                var vb = argument.Expression as VisualBasicValue<string>;
                if (vb != null)
                {
                    return vb.ExpressionText ?? string.Empty;
                }

                return string.Empty;
            }
        }
    }
}
