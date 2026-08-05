using System.Activities.Presentation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace F2B.Basic
{
    public sealed class OnlyTryDesigner : ActivityDesigner
    {
        public OnlyTryDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6)
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "Try",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            });

            var bodyPresenter = new WorkflowItemPresenter
            {
                HintText = "Drop activity here",
                MinWidth = 280,
                MinHeight = 40,
                Margin = new Thickness(0, 0, 0, 0)
            };
            BindingOperations.SetBinding(bodyPresenter, WorkflowItemPresenter.ItemProperty, new Binding("ModelItem.Body")
            {
                Mode = BindingMode.TwoWay
            });
            panel.Children.Add(bodyPresenter);

            border.Child = panel;
            Content = border;
        }
    }
}
