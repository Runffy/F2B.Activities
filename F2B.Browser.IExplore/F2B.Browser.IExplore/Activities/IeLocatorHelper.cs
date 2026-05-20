using System;
using System.Activities;
using System.Collections.Generic;

namespace F2B.Browser.IExplore
{
    internal static class IeLocatorHelper
    {
        public static IELocator BuildLocator(string selectorJson, string framePath)
        {
            if (string.IsNullOrWhiteSpace(selectorJson))
                throw new ArgumentException("Selector (Json String) is required.", nameof(selectorJson));

            return new IELocator(selectorJson.Trim(), string.IsNullOrWhiteSpace(framePath) ? null : framePath.Trim());
        }

        public static IELocator[] BuildLocators(IList<string> selectorsJson, string framePath)
        {
            if (selectorsJson == null || selectorsJson.Count == 0)
                throw new ArgumentException("Selectors (Json String) is required.", nameof(selectorsJson));

            var locators = new IELocator[selectorsJson.Count];
            for (int i = 0; i < selectorsJson.Count; i++)
                locators[i] = BuildLocator(selectorsJson[i], framePath);

            return locators;
        }

        public static string GetSelector(CodeActivityContext context, InArgument<string> selector)
        {
            var json = selector == null ? null : selector.Get(context);
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Selector (Json String) is required.");
            return json.Trim();
        }

        public static string GetElementJson(CodeActivityContext context, InArgument<string> elementJson) =>
            GetSelector(context, elementJson);

        public static string GetFramePath(CodeActivityContext context, InArgument<string> framePath)
        {
            if (framePath == null || framePath.Expression == null)
                return null;
            var value = framePath.Get(context);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
