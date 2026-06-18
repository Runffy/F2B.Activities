using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    public enum BridgeGetCookiesBaseOn
    {
        Tab,
        Browser
    }

    public enum BridgeRunJsBaseOn
    {
        Element,
        Tab
    }

    public enum BridgeSendKeysBaseOn
    {
        Tab,
        Element
    }

    public enum BridgeTakeScreenshotBaseOn
    {
        Tab,
        Element
    }

    public enum BridgeSwitchTabByType
    {
        Index,
        Title,
        TitleRegex,
        Url,
        UrlRegex,
        Tab
    }

    public enum BridgeSelectValType
    {
        Text,
        Value,
        Index
    }

    internal static class BridgeCompositeTargetResolver
    {
        public static BwElement ResolveElementTarget(
            CodeActivityContext context,
            BridgeElementTargetType targetType,
            InArgument<BwElement> elementArg,
            InArgument<string> selectorArg,
            InArgument<BwTab> inputTabArg,
            InArgument<int> delayBeforeArg,
            int timeoutMs)
        {
            var delayBefore = BridgeActivityArgumentHelper.GetOrDefault(delayBeforeArg, context, 300);

            if (targetType == BridgeElementTargetType.Element)
            {
                var element = BridgeActivityArgumentHelper.GetBwElement(elementArg, context);
                if (element == null)
                {
                    throw new InvalidOperationException(
                        "InputElement must be provided when TargetType=Element. Variable '" +
                        BridgeActivityArgumentHelper.TryGetBoundVariableName(elementArg) +
                        "' is null or not assigned.");
                }

                BridgeDelay.Apply(delayBefore);
                return element;
            }

            var selector = selectorArg == null ? null : selectorArg.Get(context);
            if (string.IsNullOrWhiteSpace(selector))
                throw new InvalidOperationException("Selector must be provided when TargetType=Selector.");

            var tab = inputTabArg == null ? null : inputTabArg.Get(context);
            return BridgeElementLocator.FindBySelector(selector, tab, 0, timeoutMs, delayBefore);
        }
    }
}
