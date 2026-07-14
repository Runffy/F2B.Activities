using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element Exists")]
    [Description("Check whether a matching element exists under ParentObject or via a full <wnd> selector.")]
    [Designer(typeof(CdpCanvasFieldsActivityDesigner))]
    public sealed class ElementExistsActivity : CodeActivity
    {
        public ElementExistsActivity()
        {
            DisplayName = "Element Exists";
        }

        [DisplayName("Parent Object")]
        [Description("Optional search root (CdpTab / CdpFrame / CdpElement). Required when Selector has no <wnd>.")]
        [Category("Input.A")]
        public InArgument<CdpBase> ParentObject { get; set; }

        [DisplayName("Selector")]
        [Description("Selector XML used to locate the element.")]
        [Category("Input.B")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Exists")]
        [Description("Outputs whether the element exists.")]
        [Category("Output")]
        public OutArgument<bool> Exists { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var root = CdpTargetResolver.GetRoot(ParentObject, context, "ParentObject");
            var selector = Selector == null ? null : Selector.Get(context);
            var exists = CdpTargetResolver.ElementExists(root, selector);
            Exists?.Set(context, exists);
        }
    }
}
