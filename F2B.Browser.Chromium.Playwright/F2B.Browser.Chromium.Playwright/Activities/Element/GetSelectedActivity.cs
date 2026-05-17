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

        protected override void Execute(CodeActivityContext context)
        {
            Selected?.Set(context, ResolveTargetElement(context).SelectGetSelected());
        }
    }
}
