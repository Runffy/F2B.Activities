using System.Activities.Presentation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using F2B.OpenRpa.Design;

namespace F2B.Basic
{
    /// <summary>
    /// Minimal canvas designer: property-grid configuration + OpenRPA collapse support.
    /// </summary>
    public sealed class BasicSimpleActivityDesigner : ActivityDesigner
    {
        public BasicSimpleActivityDesigner()
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
            ActivityDesignerCollapseHelper.Attach(this, border);
        }
    }
}
