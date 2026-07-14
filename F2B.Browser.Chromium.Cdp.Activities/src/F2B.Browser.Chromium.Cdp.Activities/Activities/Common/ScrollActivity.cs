using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Scroll")]
    [Description("Scroll a tab/frame document or an element.")]
    [Designer(typeof(CdpCanvasFieldsActivityDesigner))]
    public sealed class ScrollActivity : CodeActivity
    {
        public ScrollActivity()
        {
            DisplayName = "Scroll";
        }

        [DisplayName("Target")]
        [Category("Input.A")]
        public InArgument<CdpBase> Target { get; set; }

        [DisplayName("Selector")]
        [Category("Input.B")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Direction")]
        [Category("Input.F")]
        [DefaultValue(CdpScrollDirection.Down)]
        [TypeConverter(typeof(CdpScrollDirectionConverter))]
        public CdpScrollDirection Direction { get; set; } = CdpScrollDirection.Down;

        [DisplayName("Pixels")]
        [Category("Input.F")]
        [DefaultValue(300)]
        public InArgument<int> Pixels { get; set; } = 300;

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Delay Before (ms)")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        protected override void Execute(CodeActivityContext context)
        {
            var direction = Direction;
            var pixels = CdpActivityArgumentHelper.GetOrDefault(Pixels, context, 300);
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
                resolved.Element.Scroll(direction, pixels);
                return;
            }

            if (resolved.Kind == CdpResolvedContextKind.Frame)
            {
                resolved.Frame.Scroll(direction, pixels);
                return;
            }

            ScrollTab(resolved.Tab, direction, pixels);
        }

        private static void ScrollTab(CdpTab tab, CdpScrollDirection direction, int pixels)
        {
            var amount = Math.Max(0, pixels);
            string script;
            switch (direction)
            {
                case CdpScrollDirection.Up:
                    script = "window.scrollBy(0, -" + amount + ");";
                    break;
                case CdpScrollDirection.Left:
                    script = "window.scrollBy(-" + amount + ", 0);";
                    break;
                case CdpScrollDirection.Right:
                    script = "window.scrollBy(" + amount + ", 0);";
                    break;
                default:
                    script = "window.scrollBy(0, " + amount + ");";
                    break;
            }

            tab.RunJs(script);
        }
    }
}
