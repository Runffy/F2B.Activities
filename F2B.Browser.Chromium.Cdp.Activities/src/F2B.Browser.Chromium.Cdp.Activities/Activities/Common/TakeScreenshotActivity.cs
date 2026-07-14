using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("TakeScreenshot")]
    [Description("Capture a screenshot of a tab/frame document or an element.")]
    [Designer(typeof(CdpCanvasFieldsActivityDesigner))]
    public sealed class TakeScreenshotActivity : CodeActivity
    {
        public TakeScreenshotActivity()
        {
            DisplayName = "TakeScreenshot";
        }

        [DisplayName("Target")]
        [Category("Input.A")]
        public InArgument<CdpBase> Target { get; set; }

        [DisplayName("Selector")]
        [Category("Input.B")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Save File Path")]
        [RequiredArgument]
        [Category("Input.F")]
        public InArgument<string> SaveFilePath { get; set; }

        [DisplayName("Delay Before (ms)")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var path = SaveFilePath.Get(context);
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("Save File Path is required.");
            }

            var target = CdpTargetResolver.GetRoot(Target, context, "Target");
            var selector = Selector == null ? null : Selector.Get(context);
            var resolved = CdpTargetResolver.ResolveActionContext(
                target,
                selector,
                CdpActivityArgumentHelper.GetOrDefault(Timeout, context, 15000),
                CdpActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300),
                "Target");

            if (resolved.Kind == CdpResolvedContextKind.Element)
            {
                resolved.Element.SaveScreenshot(path);
                return;
            }

            if (resolved.Kind == CdpResolvedContextKind.Frame)
            {
                resolved.Frame.SaveScreenshot(path);
                return;
            }

            resolved.Tab.SaveScreenshot(path, fullPage: true);
        }
    }
}
