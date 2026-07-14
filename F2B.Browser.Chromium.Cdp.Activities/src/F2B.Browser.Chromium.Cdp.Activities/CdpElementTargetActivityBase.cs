using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [Designer(typeof(CdpElementTargetActivityDesigner))]
    public abstract class CdpElementTargetActivityBase : CodeActivity
    {
        protected CdpElementTargetActivityBase(string displayName)
        {
            DisplayName = displayName;
        }

        /// <summary>
        /// When true, Target must be a CdpElement (GetChildren / GetParent).
        /// </summary>
        protected virtual bool RequireCdpElementTarget
        {
            get { return false; }
        }

        /// <summary>
        /// When true, Tab/Frame with empty Selector sends keys to document body.
        /// </summary>
        protected virtual bool SendKeysBodySpecial
        {
            get { return false; }
        }

        [DisplayName("Target")]
        [Description("Optional target root (CdpTab / CdpFrame / CdpElement).")]
        [Category("Input.A")]
        public InArgument<CdpBase> Target { get; set; }

        [DisplayName("Selector")]
        [Description("Optional selector relative to Target, or a full <wnd> selector.")]
        [Category("Input.B")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Delay Before (ms)")]
        [Description("Wait time in milliseconds before execution.")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        protected CdpElement ResolveTargetElement(CodeActivityContext context)
        {
            return ResolveTargetElement(context, 15000);
        }

        protected CdpElement ResolveTargetElement(CodeActivityContext context, int timeoutMs)
        {
            var target = CdpTargetResolver.GetRoot(Target, context, "Target");
            var selector = Selector == null ? null : Selector.Get(context);
            var delayBefore = CdpActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300);
            return CdpTargetResolver.ResolveOperationElement(
                target,
                selector,
                timeoutMs,
                delayBefore,
                RequireCdpElementTarget,
                SendKeysBodySpecial);
        }

        protected CdpElement ResolveTargetElementWithTimeout(
            CodeActivityContext context,
            InArgument<int> timeout,
            int defaultTimeoutMs = 15000)
        {
            var timeoutMs = CdpActivityArgumentHelper.GetOrDefault(timeout, context, defaultTimeoutMs);
            return ResolveTargetElement(context, timeoutMs);
        }
    }
}
