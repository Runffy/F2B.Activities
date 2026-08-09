using System.Activities.Presentation;
using System.Activities.Presentation.View;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace F2B.Forms.Designers
{
    public sealed class SimpleBodyDesigner : ActivityDesigner
    {
        public SimpleBodyDesigner()
        {
            var presenter = new WorkflowItemPresenter
            {
                HintText = "Drop activities here",
                MinWidth = 280,
                MinHeight = 40
            };
            BindingOperations.SetBinding(presenter, WorkflowItemPresenter.ItemProperty, new Binding("ModelItem.Body")
            {
                Mode = BindingMode.TwoWay
            });

            Content = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                Child = presenter
            };
        }
    }
}
