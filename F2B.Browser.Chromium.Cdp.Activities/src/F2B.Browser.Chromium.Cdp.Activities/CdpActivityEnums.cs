using System;
using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    public enum CdpElementTargetType
    {
        Element,
        Selector
    }

    public enum CdpFindElementBaseOn
    {
        Tab,
        Element
    }

    public enum CdpRunJsBaseOn
    {
        Tab,
        Element
    }

    public enum CdpScrollBaseOn
    {
        Tab,
        Element
    }

    public enum CdpTakeScreenshotBaseOn
    {
        Tab,
        Element
    }

    public enum CdpNavigateType
    {
        Navigate,
        Back,
        Forward,
        Refresh
    }

    public enum CdpWaitForState
    {
        Connecting,
        Loading,
        Interactive,
        Complete
    }

    public enum CdpBrowserWindowStateOption
    {
        Maximize,
        Minimize,
        Normal
    }

    public enum CdpAttributeOperationType
    {
        Get,
        Set,
        Remove
    }

    public enum CdpStyleOperationType
    {
        Get,
        Set,
        Remove
    }

    public enum CdpPropertyOperationType
    {
        Get,
        Set
    }

    public enum CdpClassOperationType
    {
        Exists,
        Add,
        Remove
    }

    public enum CdpElementTextType
    {
        InnerText,
        OuterText,
        RawOuterText
    }

    public enum CdpActivitySelectBy
    {
        Text,
        Value,
        TextRegex,
        ValueRegex,
        Index
    }

    internal static class CdpCompositeTargetResolver
    {
        public static CdpElement ResolveElementTarget(
            CodeActivityContext context,
            CdpElementTargetType targetType,
            InArgument<CdpElement> elementArg,
            InArgument<string> selectorArg,
            InArgument<CdpTab> inputTabArg,
            InArgument<int> delayBeforeArg,
            int timeoutMs)
        {
            var delayBefore = CdpActivityArgumentHelper.GetOrDefault(delayBeforeArg, context, 300);

            if (targetType == CdpElementTargetType.Element)
            {
                var element = CdpActivityArgumentHelper.GetCdpElement(elementArg, context);
                if (element == null)
                {
                    throw new InvalidOperationException(
                        "Input Element must be provided when TargetType=Element. Variable '" +
                        CdpActivityArgumentHelper.TryGetBoundVariableName(elementArg) +
                        "' is null or not assigned.");
                }

                CdpDelay.Apply(delayBefore);
                return element;
            }

            var selector = selectorArg == null ? null : selectorArg.Get(context);
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new InvalidOperationException("Selector must be provided when TargetType=Selector.");
            }

            var tab = inputTabArg == null ? null : inputTabArg.Get(context);
            return CdpElementLocator.FindBySelector(selector, tab, 0, timeoutMs, delayBefore);
        }
    }
}
