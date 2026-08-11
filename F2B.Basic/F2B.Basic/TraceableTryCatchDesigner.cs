using System.Activities.Presentation;
using F2B.OpenRpa.Design;
using System.Activities.Presentation.View;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace F2B.Basic
{
    public sealed class TraceableTryCatchDesigner : ActivityDesigner
    {
        public TraceableTryCatchDesigner()
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
                "Try",
                "ModelItem.Try",
                "Drop Try activities here",
                expandedByDefault: true));
            panel.Children.Add(CreateExpandableCatchSection(expandedByDefault: false));
            panel.Children.Add(CreateExpandableActivitySection(
                "Finally",
                "ModelItem.Finally",
                "Drop Finally activities here (optional)",
                expandedByDefault: false));

            var hint = new TextBlock
            {
                Text = "In Catch: exception.Source is a multi-line trace (one workflow per line), from the host workflow root Sequence through nested Invoke OpenRPA.",
                FontSize = 10,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap,
                // Without MaxWidth, TextBlock DesiredWidth is the full unwrapped line and forces a huge designer.
                MaxWidth = 280,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            };
            panel.Children.Add(hint);

            border.Child = panel;
            ActivityDesignerCollapseHelper.Attach(this, border);
        }

        private FrameworkElement CreateExpandableCatchSection(bool expandedByDefault)
        {
            var presenter = new WorkflowItemPresenter
            {
                HintText = "Drop Catch activities here â€?use exception.Source in expressions",
                MinWidth = 240,
                MinHeight = 40,
                Margin = new Thickness(4, 2, 0, 0)
            };
            BindingOperations.SetBinding(presenter, WorkflowItemPresenter.ItemProperty, new Binding("ModelItem.Catch.Handler")
            {
                Mode = BindingMode.TwoWay
            });

            return CreateExpander("Catch  (argument: exception)", ActivityBodyExpandHelper.WrapExpandingBody(this, presenter), expandedByDefault);
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

            return CreateExpander(title, ActivityBodyExpandHelper.WrapExpandingBody(this, presenter), expandedByDefault);
        }

        private static Expander CreateExpander(string header, UIElement content, bool expandedByDefault)
        {
            return new Expander
            {
                Header = header,
                IsExpanded = expandedByDefault,
                Margin = new Thickness(0, 0, 0, 6),
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = content
            };
        }
    }
}
