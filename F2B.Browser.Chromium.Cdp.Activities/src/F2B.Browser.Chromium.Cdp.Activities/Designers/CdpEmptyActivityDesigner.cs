using System.Activities.Presentation;
using System.Windows;
using System.Windows.Controls;
using F2B.OpenRpa.Design;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    public sealed class CdpEmptyActivityDesigner : ActivityDesigner
    {
        public CdpEmptyActivityDesigner()
        {
            var border = new Border
            {
                Padding = new Thickness(6, 5, 6, 5),
                Child = new StackPanel()
            };
            ActivityDesignerCollapseHelper.Attach(this, border);
        }
    }
}
