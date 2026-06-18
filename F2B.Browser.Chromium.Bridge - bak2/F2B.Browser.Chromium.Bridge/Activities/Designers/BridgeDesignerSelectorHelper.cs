using System.Activities.Presentation.Model;
using F2B.Browser.Chromium.Bridge.Selectors;
using F2B.OpenRpa.Design;

namespace F2B.Browser.Chromium.Bridge
{
    internal static class BridgeDesignerSelectorHelper
    {
        public static bool SelectorHasWnd(ModelItem modelItem, string propertyName = "Selector")
        {
            return SelectorXmlSerializer.HasWndLevel(TryReadSelectorText(modelItem, propertyName));
        }

        public static string TryReadSelectorText(ModelItem modelItem, string propertyName = "Selector")
        {
            return SelectorDesignerSupport.TryReadSelectorText(modelItem, propertyName);
        }

        /// <summary>
        /// Input Tab is optional at runtime (use &lt;wnd&gt; in Selector instead).
        /// It is not shown on the activity canvas; configure it in the properties panel if needed.
        /// </summary>
        public static bool ShouldShowOptionalInputTab(ModelItem modelItem, string selectorPropertyName = "Selector")
        {
            return false;
        }
    }
}
