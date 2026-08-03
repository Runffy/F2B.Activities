using System.Activities.Presentation;
using System.Windows;
using System.Windows.Controls;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    public sealed class CdpEmptyActivityDesigner : ActivityDesigner
    {
        public CdpEmptyActivityDesigner()
        {
            Content = new Border
            {
                Padding = new Thickness(6, 5, 6, 5),
                Child = new StackPanel()
            };
        }
    }
}
