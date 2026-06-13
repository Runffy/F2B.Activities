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

        public static bool ShouldShowOptionalInputTab(ModelItem modelItem, string selectorPropertyName = "Selector")
        {
            if (modelItem == null)
                return true;

            var selectorText = TryReadSelectorText(modelItem, selectorPropertyName);
            if (string.IsNullOrWhiteSpace(selectorText))
                return true;

            return !SelectorXmlSerializer.HasWndLevel(selectorText);
        }
    }
}
