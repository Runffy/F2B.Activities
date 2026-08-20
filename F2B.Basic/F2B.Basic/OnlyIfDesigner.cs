using System;
using System.Activities.Presentation;
using System.Activities.Presentation.View;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using F2B.OpenRpa.Design;

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
                Padding = new Thickness(6),
                MinWidth = 320,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            panel.Children.Add(BasicDesignerShared.CreateLabeledExpressionEditor(
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
            System.Windows.Data.BindingOperations.SetBinding(
                thenPresenter,
                WorkflowItemPresenter.ItemProperty,
                new System.Windows.Data.Binding("ModelItem.Then")
                {
                    Mode = System.Windows.Data.BindingMode.TwoWay
                });
            panel.Children.Add(ActivityBodyExpandHelper.WrapExpandingBody(this, thenPresenter));

            border.Child = panel;
            ActivityDesignerCollapseHelper.Attach(this, border);
            Loaded += OnLoaded;
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
            BasicDesignerShared.SetRequiredBorder(
                _conditionEditorBorder,
                BasicDesignerShared.IsArgumentFilled(ModelItem, "Condition", _conditionExpressionBox));
        }
    }
}
