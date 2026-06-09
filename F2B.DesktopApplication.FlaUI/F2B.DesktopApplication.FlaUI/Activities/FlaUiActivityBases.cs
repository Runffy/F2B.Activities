using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    public enum WindowTargetType
    {
        Selector,
        Window
    }

    public enum ElementTargetType
    {
        Element,
        Selector
    }

    [DisplayName("FlaUI Window Selector Activity Base")]
    [Designer(typeof(RequiredFieldsActivityDesigner))]
    public abstract class FlaUiWindowSelectorActivityBase : CodeActivity
    {
        [DisplayName("Selector (XML)")]
        [Description("Window selector XML containing <wnd> tags only.")]
        [Category("Input.A")]
        [RequiredArgument]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(10000)]
        public InArgument<int> Timeout { get; set; } = 10000;

        [DisplayName("Interval (ms)")]
        [Category("Input.Z")]
        [DefaultValue(250)]
        public InArgument<int> Interval { get; set; } = 250;

        protected string ResolveSelector(CodeActivityContext context)
        {
            return ActivityArgumentHelper.GetRequiredSelector(Selector, context);
        }

        protected int ResolveTimeout(CodeActivityContext context)
        {
            return ActivityArgumentHelper.GetOrDefault(Timeout, context, 10000);
        }

        protected int ResolveInterval(CodeActivityContext context)
        {
            return ActivityArgumentHelper.GetOrDefault(Interval, context, 250);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            ActivityArgumentHelper.ValidateTextArgumentExpression(metadata, Selector, "Window selector XML is required.");
        }
    }

    [DisplayName("FlaUI Window Target Activity Base")]
    [TypeDescriptionProvider(typeof(FlaUiWindowTargetTypeDescriptionProvider))]
    [Designer(typeof(FlaUiWindowTargetActivityDesigner))]
    public abstract class FlaUiWindowTargetActivityBase : CodeActivity, IFlaUiWindowTargetConfig
    {
        [DisplayName("Target Type")]
        [Category("Input.A")]
        [DefaultValue(WindowTargetType.Selector)]
        [TypeConverter(typeof(FlaUiWindowTargetTypeConverter))]
        public WindowTargetType TargetType { get; set; } = WindowTargetType.Selector;

        [DisplayName("Selector (XML)")]
        [Description("Window selector XML containing <wnd> tags only.")]
        [Category("Input.B")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Input Window")]
        [Category("Input.C")]
        public InArgument<UiWindow> InputWindow { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(10000)]
        public InArgument<int> Timeout { get; set; } = 10000;

        [DisplayName("Interval (ms)")]
        [Category("Input.Z")]
        [DefaultValue(250)]
        public InArgument<int> Interval { get; set; } = 250;

        [DisplayName("Delay Before (ms)")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        protected UiWindow ResolveTargetWindow(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context);
            var client = new DesktopAutomationClient();

            if (TargetType == WindowTargetType.Window)
            {
                var window = InputWindow == null ? null : InputWindow.Get(context);
                if (window == null)
                    throw new ArgumentException("Input Window is required when TargetType=Window.");

                return window;
            }

            return client.FindWindow(
                ActivityArgumentHelper.GetRequiredSelector(Selector, context),
                ActivityArgumentHelper.GetOrDefault(Timeout, context, 10000),
                ActivityArgumentHelper.GetOrDefault(Interval, context, 250));
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            if (TargetType == WindowTargetType.Selector && !ActivityArgumentHelper.HasTextExpression(Selector))
                metadata.AddValidationError("Window selector XML is required when TargetType=Selector.");
        }
    }

    [DisplayName("FlaUI Selector Activity Base")]
    [Designer(typeof(RequiredFieldsActivityDesigner))]
    public abstract class FlaUiSelectorActivityBase : CodeActivity
    {
        [DisplayName("Input Window")]
        [Description("Optional. When set, selector must contain <ctrl> tags only.")]
        [Category("Input.A")]
        public InArgument<UiWindow> InputWindow { get; set; }

        [DisplayName("Selector (XML)")]
        [Description("Full selector XML, or ctrl-only selector when Input Window is provided.")]
        [Category("Input.B")]
        [RequiredArgument]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(10000)]
        public InArgument<int> Timeout { get; set; } = 10000;

        [DisplayName("Interval (ms)")]
        [Category("Input.Z")]
        [DefaultValue(250)]
        public InArgument<int> Interval { get; set; } = 250;

        protected UiWindow ResolveInputWindow(CodeActivityContext context)
        {
            return InputWindow == null ? null : InputWindow.Get(context);
        }

        protected string ResolveSelector(CodeActivityContext context)
        {
            return ActivityArgumentHelper.GetRequiredSelector(Selector, context);
        }

        protected int ResolveTimeout(CodeActivityContext context)
        {
            return ActivityArgumentHelper.GetOrDefault(Timeout, context, 10000);
        }

        protected int ResolveInterval(CodeActivityContext context)
        {
            return ActivityArgumentHelper.GetOrDefault(Interval, context, 250);
        }

        protected DesktopAutomationClient CreateClient()
        {
            return new DesktopAutomationClient();
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            ActivityArgumentHelper.ValidateTextArgumentExpression(metadata, Selector, "Selector XML is required.");
        }
    }

    [DisplayName("FlaUI Element Target Activity Base")]
    [TypeDescriptionProvider(typeof(FlaUiElementTargetTypeDescriptionProvider))]
    [Designer(typeof(FlaUiElementTargetActivityDesigner))]
    public abstract class FlaUiElementTargetActivityBase : CodeActivity, IFlaUiElementTargetConfig
    {
        [DisplayName("Target Type")]
        [Category("Input.A")]
        [DefaultValue(ElementTargetType.Selector)]
        [TypeConverter(typeof(FlaUiElementTargetTypeConverter))]
        public ElementTargetType TargetType { get; set; } = ElementTargetType.Selector;

        [DisplayName("Input Window")]
        [Description("Optional. When set, selector must contain <ctrl> tags only.")]
        [Category("Input.B")]
        public InArgument<UiWindow> InputWindow { get; set; }

        [DisplayName("Selector (XML)")]
        [Description("Full selector XML, or ctrl-only selector when Input Window is provided.")]
        [Category("Input.C")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Input Element")]
        [Category("Input.D")]
        public InArgument<UiElement> Element { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(10000)]
        public InArgument<int> Timeout { get; set; } = 10000;

        [DisplayName("Interval (ms)")]
        [Category("Input.Z")]
        [DefaultValue(250)]
        public InArgument<int> Interval { get; set; } = 250;

        [DisplayName("Delay Before (ms)")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        protected UiElement ResolveTargetElement(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context);

            if (TargetType == ElementTargetType.Element)
            {
                var element = Element == null ? null : Element.Get(context);
                if (element == null)
                    throw new ArgumentException("Input Element is required when TargetType=Element.");

                return element;
            }

            var client = new DesktopAutomationClient();
            return client.FindElement(
                ActivityArgumentHelper.GetRequiredSelector(Selector, context),
                ActivityArgumentHelper.GetOrDefault(Timeout, context, 10000),
                ActivityArgumentHelper.GetOrDefault(Interval, context, 250),
                ResolveInputWindow(context));
        }

        protected UiWindow ResolveInputWindow(CodeActivityContext context)
        {
            return InputWindow == null ? null : InputWindow.Get(context);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            if (TargetType == ElementTargetType.Selector && !ActivityArgumentHelper.HasTextExpression(Selector))
                metadata.AddValidationError("Selector XML is required when TargetType=Selector.");
        }
    }
}
