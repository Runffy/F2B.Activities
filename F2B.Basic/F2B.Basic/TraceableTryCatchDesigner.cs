using System.Activities.Presentation;
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
                MinWidth = 320
            };

            var panel = new StackPanel();
            panel.Children.Add(CreateActivitySection("Try", "ModelItem.Try", "Drop Try activities here"));
            panel.Children.Add(CreateCatchSection());
            panel.Children.Add(CreateActivitySection("Finally", "ModelItem.Finally", "Drop Finally activities here (optional)"));

            var hint = new TextBlock
            {
                Text = "In Catch: exception.Source = DisplayName path (e.g. Try/Sequence[1]/some loop/error point). FaultXPath keeps type path.",
                FontSize = 10,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };
            panel.Children.Add(hint);

            border.Child = panel;
            Content = border;
        }

        private static FrameworkElement CreateCatchSection()
        {
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            section.Children.Add(new TextBlock
            {
                Text = "Catch  (argument: exception)",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            });

            var presenter = new WorkflowItemPresenter
            {
                HintText = "Drop Catch activities here — use exception.Source in expressions",
                MinWidth = 280,
                MinHeight = 40
            };
            // ActivityAction<Exception>.Handler — same pattern as OpenRPA ForEach Body.Handler
            BindingOperations.SetBinding(presenter, WorkflowItemPresenter.ItemProperty, new Binding("ModelItem.Catch.Handler")
            {
                Mode = BindingMode.TwoWay
            });
            section.Children.Add(presenter);
            return section;
        }

        private static FrameworkElement CreateActivitySection(string title, string bindingPath, string hint)
        {
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            section.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            });

            var presenter = new WorkflowItemPresenter
            {
                HintText = hint,
                MinWidth = 280,
                MinHeight = 40
            };
            BindingOperations.SetBinding(presenter, WorkflowItemPresenter.ItemProperty, new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay
            });
            section.Children.Add(presenter);
            return section;
        }
    }
}
