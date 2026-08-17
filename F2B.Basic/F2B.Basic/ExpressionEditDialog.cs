using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace F2B.Basic
{
    /// <summary>
    /// Resizable dialog for editing a long InArgument expression (VB) more comfortably.
    /// </summary>
    internal static class ExpressionEditDialog
    {
        internal static void Show(
            Window owner,
            ActivityDesigner designer,
            string title,
            Type expressionType,
            ModelItem argumentOwner,
            string argumentPropertyName)
        {
            if (designer == null || argumentOwner == null || string.IsNullOrWhiteSpace(argumentPropertyName))
            {
                return;
            }

            var expressionBox = new ExpressionTextBox
            {
                HintText = "Edit expression",
                ExpressionType = expressionType,
                MinLines = 6,
                MaxLines = 30,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0)
            };

            BindingOperations.SetBinding(
                expressionBox,
                ExpressionTextBox.OwnerActivityProperty,
                new Binding("ModelItem") { Source = designer });

            BindingOperations.SetBinding(
                expressionBox,
                ExpressionTextBox.ExpressionProperty,
                new Binding(argumentPropertyName)
                {
                    Source = argumentOwner,
                    Mode = BindingMode.TwoWay,
                    Converter = new ArgumentToExpressionConverter(),
                    ConverterParameter = "In"
                });

            var host = new Border
            {
                Padding = new Thickness(10),
                Child = expressionBox
            };

            var window = new Window
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Edit Expression" : title,
                Content = host,
                Width = 640,
                Height = 360,
                MinWidth = 420,
                MinHeight = 240,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                ShowInTaskbar = false,
                Background = Brushes.White
            };

            if (owner != null)
            {
                window.Owner = owner;
            }
            else
            {
                try
                {
                    window.Owner = Application.Current?.MainWindow;
                }
                catch
                {
                    // ignore
                }
            }

            window.Loaded += (s, e) =>
            {
                expressionBox.Focus();
                Keyboard.Focus(expressionBox);
            };

            window.ShowDialog();
        }

        internal static Window FindOwnerWindow(DependencyObject from)
        {
            DependencyObject current = from;
            while (current != null)
            {
                if (current is Window window)
                {
                    return window;
                }

                current = LogicalTreeHelper.GetParent(current)
                    ?? VisualTreeHelper.GetParent(current);
            }

            try
            {
                return Application.Current?.MainWindow;
            }
            catch
            {
                return null;
            }
        }
    }
}
