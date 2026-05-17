using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Element Text")]
    [Description("Get the text content of the target element.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class ElementGetTextActivity : ElementTargetActivityBase
    {
        [DisplayName("Text")]
        [Description("Outputs the element text content.")]
        [Category("Output")]
        public OutArgument<string> Text { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Text?.Set(context, ResolveTargetElement(context).GetText());
        }
    }
}
