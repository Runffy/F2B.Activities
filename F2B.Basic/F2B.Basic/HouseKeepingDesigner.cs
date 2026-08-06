using System;
using System.Activities.Presentation;
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
    public sealed class HouseKeepingDesigner : ActivityDesigner
    {
        private readonly Border _beforeBorder;

        public HouseKeepingDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6)
            };

            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = "Before",
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            });

            var expressionTextBox = new ExpressionTextBox
            {
                Width = 220,
                MaxWidth = 220,
                HintText = "DateTime expression",
                ExpressionType = typeof(DateTime),
                MinLines = 1,
                MaxLines = 1
            };
            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.ExpressionProperty, new Binding("ModelItem.Before")
            {
                Mode = BindingMode.TwoWay,
                Converter = new ArgumentToExpressionConverter(),
                ConverterParameter = "In"
            });

            _beforeBorder = new Border
            {
                Margin = new Thickness(4, 0, 0, 0),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = expressionTextBox
            };
            row.Children.Add(_beforeBorder);

            border.Child = row;
            Content = border;
            Loaded += OnLoaded;
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
            var property = ModelItem?.Properties["Before"];
            bool filled = property != null && property.IsSet && property.Value != null;
            if (filled)
            {
                _beforeBorder.BorderBrush = Brushes.Transparent;
                _beforeBorder.BorderThickness = new Thickness(0);
            }
            else
            {
                _beforeBorder.BorderBrush = Brushes.Red;
                _beforeBorder.BorderThickness = new Thickness(1);
            }
        }
    }
}
