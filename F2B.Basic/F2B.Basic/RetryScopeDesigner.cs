using System.Activities.Presentation;
using F2B.OpenRpa.Design;
using System.Activities.Presentation.View;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace F2B.Basic
{
    public sealed class RetryScopeDesigner : ActivityDesigner
    {
        public RetryScopeDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                MinWidth = 300,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            panel.Children.Add(CreateExpandableActivitySection(
                "Retry Body",
                "ModelItem.RetryBody.Handler",
                "Drop activities to retry here (use Retry Counter, default: retry_counter)",
                expandedByDefault: true));
            panel.Children.Add(CreateExpandableActivitySection(
                "Assert Body",
                "ModelItem.AssertBody.Handler",
                "Drop assert / verify activities here (fault triggers retry)",
                expandedByDefault: true));

            var hint = new TextBlock
            {
                Text = "Fault in Retry Body or Assert Body retries after Retry Interval until Retry Time limit (By Times / By Timeout). Retry Counter (default retry_counter) is 1-based.",
                FontSize = 10,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 280,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            };
            panel.Children.Add(hint);

            border.Child = panel;
            ActivityDesignerCollapseHelper.Attach(this, border);
        }

        private FrameworkElement CreateExpandableActivitySection(
            string title,
            string bindingPath,
            string hint,
            bool expandedByDefault)
        {
            var presenter = new WorkflowItemPresenter
            {
                HintText = hint,
                MinWidth = 240,
                MinHeight = 40,
                Margin = new Thickness(4, 2, 0, 0)
            };
            BindingOperations.SetBinding(presenter, WorkflowItemPresenter.ItemProperty, new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay
            });

            return new Expander
            {
                Header = title,
                IsExpanded = expandedByDefault,
                Margin = new Thickness(0, 0, 0, 6),
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = ActivityBodyExpandHelper.WrapExpandingBody(this, presenter)
            };
        }
    }
}
