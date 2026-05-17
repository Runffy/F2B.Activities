using System.Activities.Presentation;
using System.Activities.Presentation.View;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace F2B.Basic
{
    public sealed class UnitTestScopeDesigner : ActivityDesigner
    {
        public UnitTestScopeDesigner()
        {
            var root = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4)
            };

            var presenter = new WorkflowItemPresenter
            {
                HintText = "Drop activities here",
                Margin = new Thickness(0)
            };
            BindingOperations.SetBinding(presenter, WorkflowItemPresenter.ItemProperty, new Binding("ModelItem.Body")
            {
                Mode = BindingMode.TwoWay
            });

            root.Child = presenter;
            Content = root;
        }
    }
}
