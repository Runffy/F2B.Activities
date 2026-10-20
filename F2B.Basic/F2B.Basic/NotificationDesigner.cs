using System;
using System.Activities.Presentation;
using F2B.OpenRpa.Design;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.View;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace F2B.Basic
{
    public sealed class NotificationDesigner : ActivityDesigner
    {
        private readonly Border _messageBorder;

        public NotificationDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6)
            };

            var panel = new StackPanel();
            panel.Children.Add(CreateMessageRow(out _messageBorder));

            border.Child = panel;
            ActivityDesignerCollapseHelper.Attach(this, border);
            Loaded += OnLoaded;
        }

        private static FrameworkElement CreateMessageRow(out Border editorBorder)
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = "Message",
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            });

            var expressionTextBox = new ExpressionTextBox
            {
                Width = 220,
                MaxWidth = 220,
                HintText = "Notification text",
                ExpressionType = typeof(string),
                MinLines = 1,
                MaxLines = 2
            };
            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.ExpressionProperty, new Binding("ModelItem.Message")
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

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorder();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorder), DispatcherPriority.Background);
        }

        private void RefreshRequiredBorder()
        {
            var property = ModelItem?.Properties["Message"];
            bool filled = property != null && property.IsSet && property.Value != null;
            if (filled)
            {
                _messageBorder.BorderBrush = Brushes.Transparent;
                _messageBorder.BorderThickness = new Thickness(0);
            }
            else
            {
                _messageBorder.BorderBrush = Brushes.Red;
                _messageBorder.BorderThickness = new Thickness(1);
            }
        }
    }
}
