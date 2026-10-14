using System.Activities.Presentation;
using System.Activities.Presentation.View;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using F2B.OpenRpa.Design;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    public sealed class ListenStepsActivityDesigner : ActivityDesigner
    {
        public ListenStepsActivityDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                MinWidth = 300,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var presenter = new WorkflowItemPresenter
            {
                HintText = "Drop activities here (Packets is the current batch)",
                MinWidth = 240,
                MinHeight = 40,
                Margin = new Thickness(4, 2, 0, 0)
            };
            BindingOperations.SetBinding(presenter, WorkflowItemPresenter.ItemProperty, new Binding("ModelItem.Body.Handler")
            {
                Mode = BindingMode.TwoWay
            });

            var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            panel.Children.Add(new TextBlock
            {
                Text = "Body",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });
            panel.Children.Add(presenter);
            panel.Children.Add(new TextBlock
            {
                Text = "Listen-Break exits the loop. Listen-Continue skips to the next batch. Neither stops listen.",
                FontSize = 10,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 280,
                Margin = new Thickness(0, 6, 0, 0)
            });

            border.Child = panel;
            ActivityDesignerCollapseHelper.Attach(this, border);
        }
    }
}
