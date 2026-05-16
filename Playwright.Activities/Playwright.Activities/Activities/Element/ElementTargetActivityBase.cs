using System;
using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    public enum ElementTargetType
    {
        Element,
        Selector
    }

    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [Designer(typeof(ElementTargetActivityDesigner))]
    public abstract class ElementTargetActivityBase : CodeActivity, IElementTargetConfig
    {
        [Category("Target")]
        public InArgument<PwElement> Element { get; set; }

        [Category("Target")]
        public InArgument<string> Selector { get; set; }

        [Category("Target")]
        [DisplayName("Tab")]
        public InArgument<PwTab> InputTab { get; set; }

        [Category("Target")]
        [DefaultValue(ElementTargetType.Selector)]
        [TypeConverter("Playwright.Activities.ElementTargetTypeConverter, Playwright.Activities")]
        public ElementTargetType TargetType
        {
            get => _targetType;
            set
            {
                _targetType = value;
                TypeDescriptor.Refresh(this);
            }
        }

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
                    throw new ArgumentException("TargetType=Element 时必须提供 Element。");
                }

                PlaywrightSyncClient.ApplyDelay(delayBefore);
                return element;
            }

            var selector = Selector == null ? null : Selector.Get(context);
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new ArgumentException("TargetType=Selector 时必须提供 Selector。");
            }

            var tab = InputTab == null ? null : InputTab.Get(context);
            if (tab == null)
            {
                throw new ArgumentException("TargetType=Selector 时必须提供 Tab。");
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
                    metadata.AddValidationError("TargetType=Element 时必须填写 Element。");
                }
            }
            else if (TargetType == ElementTargetType.Selector)
            {
                if (Selector == null || Selector.Expression == null)
                {
                    metadata.AddValidationError("TargetType=Selector 时必须填写 Selector。");
                }

                if (InputTab == null || InputTab.Expression == null)
                {
                    metadata.AddValidationError("TargetType=Selector 时必须填写 Tab。");
                }
            }
            else
            {
                metadata.AddValidationError("不支持的 TargetType。");
            }
        }
    }
}
