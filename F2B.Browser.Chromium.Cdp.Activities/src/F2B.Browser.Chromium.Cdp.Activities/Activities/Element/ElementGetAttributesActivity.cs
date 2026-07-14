using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-GetAttributes")]
    [Description("Get all attributes from the target element.")]
    public sealed class ElementGetAttributesActivity : CdpElementTargetActivityBase
    {
        public ElementGetAttributesActivity()
            : base("Element-GetAttributes")
        {
        }

        [DisplayName("Attr Dict")]
        [Description("Outputs all attributes as a dictionary.")]
        [Category("Output")]
        public OutArgument<Dictionary<string, string>> AttrDict { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var element = ResolveTargetElementWithTimeout(context, Timeout);
            AttrDict?.Set(context, element.Attrs);
        }
    }
}
