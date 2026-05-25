using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Selected")]
    [Description("Get the currently selected options of a select element.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class GetSelectedActivity : ElementTargetActivityBase
    {
        [DisplayName("Selected")]
        [Description("Outputs details of selected options.")]
        [Category("Output")]
        public OutArgument<List<Dictionary<string, object>>> Selected { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            Selected?.Set(context, ResolveTargetElementWithTimeout(context, Timeout).SelectGetSelected());
        }
    }
}
