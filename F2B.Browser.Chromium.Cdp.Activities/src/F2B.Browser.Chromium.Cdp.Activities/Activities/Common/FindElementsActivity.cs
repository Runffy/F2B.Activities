using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Find Elements")]
    [Description("Query all matching elements under ParentObject or via a full <wnd> selector.")]
    [Designer(typeof(CdpCanvasFieldsActivityDesigner))]
    public sealed class FindElementsActivity : CodeActivity
    {
        public FindElementsActivity()
        {
            DisplayName = "Find Elements";
        }

        [DisplayName("Parent Object")]
        [Description("Optional search root (CdpTab / CdpFrame / CdpElement). Required when Selector has no <wnd>.")]
        [Category("Input.A")]
        public InArgument<CdpBase> ParentObject { get; set; }

        [DisplayName("Selector")]
        [Description("Selector XML used to locate matching elements.")]
        [Category("Input.B")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Throw Exception")]
        [Description("When true, throws if no elements are found.")]
        [Category("Input.Z")]
        [DefaultValue(false)]
        public InArgument<bool> ThrowException { get; set; } = false;

        [DisplayName("Elements")]
        [Description("Outputs all currently matching elements.")]
        [Category("Output")]
        public OutArgument<CdpElement[]> Elements { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var root = CdpTargetResolver.GetRoot(ParentObject, context, "ParentObject");
            var selector = Selector == null ? null : Selector.Get(context);
            var throwException = CdpActivityArgumentHelper.GetOrDefault(ThrowException, context, false);
            var found = CdpTargetResolver.FindElements(root, selector);
            if (throwException && (found == null || found.Length == 0))
            {
                throw new InvalidOperationException("No elements matched the selector.");
            }

            Elements?.Set(context, found ?? new CdpElement[0]);
        }
    }
}
