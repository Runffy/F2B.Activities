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
    public sealed class OnlyIfDesigner : ActivityDesigner
    {
        private readonly Border _conditionEditorBorder;
        private readonly ExpressionTextBox _conditionExpressionBox;

        public OnlyIfDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6)
            };

            var panel = new StackPanel();
            panel.Children.Add(CreateLabeledExpressionEditor(
                "Condition",
                "ModelItem.Condition",
                typeof(bool),
                "Boolean expression",
                out _conditionEditorBorder,
                out _conditionExpressionBox));

            panel.Children.Add(new TextBlock
            {
                Text = "Then",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 2)
            });

            var thenPresenter = new WorkflowItemPresenter
            {
                HintText = "Drop activity here",
                MinWidth = 280,
                MinHeight = 40,
                Margin = new Thickness(0, 0, 0, 0)
            };
            BindingOperations.SetBinding(thenPresenter, WorkflowItemPresenter.ItemProperty, new Binding("ModelItem.Then")
            {
                Mode = BindingMode.TwoWay
            });
            panel.Children.Add(thenPresenter);

            border.Child = panel;
            Content = border;
            Loaded += OnLoaded;
        }

        private static FrameworkElement CreateLabeledExpressionEditor(
            string label,
            string bindingPath,
            Type expressionType,
            string hint,
            out Border editorBorder,
            out ExpressionTextBox expressionTextBox)
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
                Width = 200,
                MaxWidth = 200,
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

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
        }

        private void RefreshRequiredBorders()
        {
            // Condition is optional (false/empty skips Then); no required red border.
            SetRequiredBorder(_conditionEditorBorder, true);
        }

        private static void SetRequiredBorder(Border border, bool filled)
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
