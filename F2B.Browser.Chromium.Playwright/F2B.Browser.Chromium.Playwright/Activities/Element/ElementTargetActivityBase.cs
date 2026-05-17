using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    public enum ElementTargetType
    {
        Element,
        Selector
    }

    [DisplayName("Element Target Base")]
    [Description("Provides unified target resolution for element activities.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [Designer(typeof(ElementTargetActivityDesigner))]
    public abstract class ElementTargetActivityBase : CodeActivity, IElementTargetConfig
    {
        [DisplayName("Element")]
        [Description("Element object to operate on directly.")]
        [Category("Target")]
        public InArgument<PwElement> Element { get; set; }

        [DisplayName("Selector")]
        [Description("Selector used to locate the target element.")]
        [Category("Target")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Input Tab")]
        [Description("Tab instance used when locating by selector.")]
        [Category("Target")]
        public InArgument<PwTab> InputTab { get; set; }

        [DisplayName("Target Type")]
        [Description("Choose whether to resolve target by element or selector.")]
        [Category("Target")]
        [DefaultValue(ElementTargetType.Selector)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.ElementTargetTypeConverter, F2B.Browser.Chromium.Playwright")]
        public ElementTargetType TargetType
        {
            get => _targetType;
            set
            {
                _targetType = value;
                TypeDescriptor.Refresh(this);
            }
        }

        [DisplayName("Delay Before")]
        [Description("Wait time in milliseconds before execution.")]
        [Category("Time")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        private ElementTargetType _targetType = ElementTargetType.Selector;

        protected PwElement ResolveTargetElement(CodeActivityContext context)
        {
            var delayBefore = ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300);
            if (TargetType == ElementTargetType.Element)
            {
                var element = Element == null ? null : Element.Get(context);
                if (element == null)
                {
                    throw new ArgumentException("Element must be provided when TargetType=Element.");
                }

                PlaywrightSyncClient.ApplyDelay(delayBefore);
                return element;
            }

            var selector = Selector == null ? null : Selector.Get(context);
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new ArgumentException("Selector must be provided when TargetType=Selector.");
            }

            var tab = InputTab == null ? null : InputTab.Get(context);
            if (tab == null)
            {
                throw new ArgumentException("Tab must be provided when TargetType=Selector.");
            }

            return tab.FindElement(selector, index: 0, timeout: null, waitState: null, delayBefore: delayBefore);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            if (TargetType == ElementTargetType.Element)
            {
                if (Element == null || Element.Expression == null)
                {
                    metadata.AddValidationError("Element must be provided when TargetType=Element.");
                }
            }
            else if (TargetType == ElementTargetType.Selector)
            {
                if (Selector == null || Selector.Expression == null)
                {
                    metadata.AddValidationError("Selector must be provided when TargetType=Selector.");
                }

                if (InputTab == null || InputTab.Expression == null)
                {
                    metadata.AddValidationError("Tab must be provided when TargetType=Selector.");
                }
            }
            else
            {
                metadata.AddValidationError("Unsupported TargetType.");
            }
        }
    }
}
