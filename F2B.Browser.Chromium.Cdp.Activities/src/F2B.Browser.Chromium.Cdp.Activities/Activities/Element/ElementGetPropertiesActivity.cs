using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-GetProperties")]
    [Description("Get readable DOM/JS properties from the target element as a dictionary.")]
    public sealed class ElementGetPropertiesActivity : CdpElementTargetActivityBase
    {
        public ElementGetPropertiesActivity()
            : base("Element-GetProperties")
        {
        }

        [DisplayName("Prop Dict")]
        [Description("Outputs properties as a dictionary.")]
        [Category("Output")]
        public OutArgument<Dictionary<string, string>> PropDict { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var element = ResolveTargetElementWithTimeout(context, Timeout);
            PropDict?.Set(context, element.Properties);
        }
    }
}
