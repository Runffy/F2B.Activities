using System;
using System.Activities;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Selectors;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    internal enum CdpResolvedContextKind
    {
        Tab,
        Frame,
        Element
    }

    internal sealed class CdpResolvedContext
    {
        public CdpResolvedContextKind Kind { get; set; }
        public CdpTab Tab { get; set; }
        public CdpFrame Frame { get; set; }
        public CdpElement Element { get; set; }

        public CdpBase AsBase()
        {
            if (Kind == CdpResolvedContextKind.Element)
            {
                return Element;
            }

            if (Kind == CdpResolvedContextKind.Frame)
            {
                return Frame;
            }

            return Tab;
        }
    }

    internal static class CdpTargetResolver
    {
        public static CdpBase GetRoot(InArgument<CdpBase> rootArgument, CodeActivityContext context, string displayName)
        {
            if (rootArgument == null)
            {
                return null;
            }

            var root = rootArgument.Get(context);
            if (root == null)
            {
                return null;
            }

            if (!(root is CdpTab) && !(root is CdpFrame) && !(root is CdpElement))
            {
                throw new InvalidOperationException(
                    displayName + " must be a CdpTab, CdpFrame, or CdpElement.");
            }

            return root;
        }

        public static string NormalizeSelector(CdpBase root, string selector)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                return null;
            }

            if (root != null && SelectorXmlSerializer.HasWndLevel(selector))
            {
                return SelectorXmlSerializer.ToOperationXml(SelectorXmlSerializer.SplitScope(selector));
            }

            return selector.Trim();
        }

        public static void EnsureFindInputs(CdpBase root, string selector, string rootDisplayName)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new InvalidOperationException("Selector is required.");
            }

            if (root == null && !SelectorXmlSerializer.HasWndLevel(selector))
            {
                throw new InvalidOperationException(
                    "Provide " + rootDisplayName + ", or a Selector that contains <wnd>.");
            }
        }

        public static CdpElement FindElement(
            CdpBase root,
            string selector,
            int timeoutMs,
            bool throwException,
            int delayBefore = 0)
        {
            EnsureFindInputs(root, selector, "ParentObject");
            CdpDelay.Apply(delayBefore);
            selector = NormalizeSelector(root, selector);

            if (root == null)
            {
                return CdpElementLocator.FindBySelector(selector, null, 0, timeoutMs, 0, throwException);
            }

            return root.FindElement(selector, timeoutMs, throwException);
        }

        public static CdpElement[] FindElements(CdpBase root, string selector)
        {
            EnsureFindInputs(root, selector, "ParentObject");
            selector = NormalizeSelector(root, selector);

            if (root == null)
            {
                return CdpElementLocator.FindAllBySelector(selector, null, null);
            }

            return root.FindElements(selector);
        }

        public static bool ElementExists(CdpBase root, string selector)
        {
            EnsureFindInputs(root, selector, "ParentObject");
            selector = NormalizeSelector(root, selector);

            if (root == null)
            {
                return CdpElementLocator.Exists(selector, null);
            }

            return root.ElementExists(selector);
        }

        public static CdpFrame FindFrame(
            CdpBase root,
            string selector,
            int timeoutMs,
            bool throwException,
            int delayBefore = 0)
        {
            EnsureFindInputs(root, selector, "ParentObject");
            CdpDelay.Apply(delayBefore);
            selector = NormalizeSelector(root, selector);

            if (root == null)
            {
                var tab = CdpTabFinder.FindTab(selector).Tab;
                var operationXml = SelectorXmlSerializer.HasWndLevel(selector)
                    ? SelectorXmlSerializer.ToOperationXml(SelectorXmlSerializer.SplitScope(selector))
                    : selector;
                return tab.FindFrame(operationXml, timeoutMs, throwException);
            }

            return root.FindFrame(selector, timeoutMs, throwException);
        }

        /// <summary>Mode B: resolve Tab/Frame document or Element for RunJs/Scroll/Screenshot.</summary>
        public static CdpResolvedContext ResolveActionContext(
            CdpBase target,
            string selector,
            int timeoutMs,
            int delayBefore,
            string targetDisplayName)
        {
            if (target == null && string.IsNullOrWhiteSpace(selector))
            {
                throw new InvalidOperationException(
                    "Provide " + targetDisplayName + ", or a Selector that contains <wnd>.");
            }

            if (target == null && !SelectorXmlSerializer.HasWndLevel(selector))
            {
                throw new InvalidOperationException(
                    "Provide " + targetDisplayName + ", or a Selector that contains <wnd>.");
            }

            CdpDelay.Apply(delayBefore);

            if (target != null && string.IsNullOrWhiteSpace(selector))
            {
                return FromRootAlone(target);
            }

            if (target != null)
            {
                var relative = NormalizeSelector(target, selector);
                var element = target.FindElement(relative, timeoutMs, true);
                return new CdpResolvedContext
                {
                    Kind = CdpResolvedContextKind.Element,
                    Element = element,
                    Tab = element.Tab
                };
            }

            // null target + full selector
            var scope = SelectorXmlSerializer.SplitScope(selector);
            var tab = CdpTabFinder.FindTab(selector).Tab;
            var operationXml = SelectorXmlSerializer.ToOperationXml(scope);
            if (string.IsNullOrWhiteSpace(operationXml) ||
                (scope.ElementLevels == null || scope.ElementLevels.Count == 0) &&
                (scope.FrameLevels == null || scope.FrameLevels.Count == 0))
            {
                return new CdpResolvedContext { Kind = CdpResolvedContextKind.Tab, Tab = tab };
            }

            if ((scope.ElementLevels == null || scope.ElementLevels.Count == 0) &&
                scope.FrameLevels != null && scope.FrameLevels.Count > 0)
            {
                var frame = tab.FindFrame(operationXml, timeoutMs, true);
                return new CdpResolvedContext
                {
                    Kind = CdpResolvedContextKind.Frame,
                    Frame = frame,
                    Tab = tab
                };
            }

            var found = tab.FindElement(operationXml, timeoutMs, true);
            return new CdpResolvedContext
            {
                Kind = CdpResolvedContextKind.Element,
                Element = found,
                Tab = tab
            };
        }

        /// <summary>Mode C: resolve a CdpElement for Element-* activities.</summary>
        public static CdpElement ResolveOperationElement(
            CdpBase target,
            string selector,
            int timeoutMs,
            int delayBefore,
            bool requireCdpElementTarget,
            bool sendKeysBodySpecial)
        {
            if (requireCdpElementTarget)
            {
                var element = target as CdpElement;
                if (element == null)
                {
                    throw new InvalidOperationException("Target is required and must be a CdpElement.");
                }

                CdpDelay.Apply(delayBefore);
                if (string.IsNullOrWhiteSpace(selector))
                {
                    return element;
                }

                return element.FindElement(NormalizeSelector(element, selector), timeoutMs, true);
            }

            if (target == null && string.IsNullOrWhiteSpace(selector))
            {
                throw new InvalidOperationException(
                    "Provide Target as Element/Frame, or a Selector that contains <wnd> and resolves to an element.");
            }

            if (target == null && !SelectorXmlSerializer.HasWndLevel(selector))
            {
                throw new InvalidOperationException(
                    "Provide Target, or a Selector that contains <wnd>.");
            }

            CdpDelay.Apply(delayBefore);

            if (sendKeysBodySpecial && target != null && string.IsNullOrWhiteSpace(selector))
            {
                if (target is CdpTab || target is CdpFrame)
                {
                    return target.FindElement("<ctrl tag=\"body\" />", timeoutMs, true);
                }
            }

            if (target is CdpFrame && string.IsNullOrWhiteSpace(selector))
            {
                var frame = (CdpFrame)target;
                if (frame.Element == null)
                {
                    throw new InvalidOperationException("Frame host element is not available.");
                }

                return frame.Element;
            }

            if (target is CdpElement && string.IsNullOrWhiteSpace(selector))
            {
                return (CdpElement)target;
            }

            if (target is CdpTab && string.IsNullOrWhiteSpace(selector))
            {
                throw new InvalidOperationException(
                    "Element activity requires a CdpElement (provide Target as Element, or a Selector that resolves to an element).");
            }

            if (target != null)
            {
                return target.FindElement(NormalizeSelector(target, selector), timeoutMs, true);
            }

            var scope = SelectorXmlSerializer.SplitScope(selector);
            var tab = CdpTabFinder.FindTab(selector).Tab;
            var operationXml = SelectorXmlSerializer.ToOperationXml(scope);

            if (sendKeysBodySpecial &&
                (scope.ElementLevels == null || scope.ElementLevels.Count == 0) &&
                (scope.FrameLevels == null || scope.FrameLevels.Count == 0))
            {
                return tab.FindElement("<ctrl tag=\"body\" />", timeoutMs, true);
            }

            if (sendKeysBodySpecial &&
                (scope.ElementLevels == null || scope.ElementLevels.Count == 0) &&
                scope.FrameLevels != null && scope.FrameLevels.Count > 0)
            {
                var frame = tab.FindFrame(operationXml, timeoutMs, true);
                return frame.FindElement("<ctrl tag=\"body\" />", timeoutMs, true);
            }

            if (scope.ElementLevels == null || scope.ElementLevels.Count == 0)
            {
                throw new InvalidOperationException(
                    "Selector must resolve to an element (include a <ctrl> level).");
            }

            return tab.FindElement(operationXml, timeoutMs, true);
        }

        private static CdpResolvedContext FromRootAlone(CdpBase target)
        {
            var element = target as CdpElement;
            if (element != null)
            {
                return new CdpResolvedContext
                {
                    Kind = CdpResolvedContextKind.Element,
                    Element = element,
                    Tab = element.Tab
                };
            }

            var frame = target as CdpFrame;
            if (frame != null)
            {
                return new CdpResolvedContext
                {
                    Kind = CdpResolvedContextKind.Frame,
                    Frame = frame,
                    Tab = frame.Tab
                };
            }

            var tab = target as CdpTab;
            if (tab != null)
            {
                return new CdpResolvedContext
                {
                    Kind = CdpResolvedContextKind.Tab,
                    Tab = tab
                };
            }

            throw new InvalidOperationException("Target must be a CdpTab, CdpFrame, or CdpElement.");
        }
    }
}
