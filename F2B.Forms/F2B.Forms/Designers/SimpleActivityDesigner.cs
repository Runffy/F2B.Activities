using System.Activities.Presentation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace F2B.Forms.Designers
{
    public sealed class SimpleActivityDesigner : ActivityDesigner
    {
        public SimpleActivityDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                MinWidth = 220,
                Child = new TextBlock
                {
                    Text = "Configure properties in the Property Grid.",
                    FontSize = 11,
                    Foreground = Brushes.DimGray,
                    TextWrapping = TextWrapping.Wrap
                }
            };
            Content = border;
        }
    }
}
