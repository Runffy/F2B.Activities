using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Input Value")]
    [Description("Get the current value of an input element.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class GetInputValueActivity : ElementTargetActivityBase
    {
        [DisplayName("Value")]
        [Description("Outputs the current input value.")]
        [Category("Output")]
        public OutArgument<string> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Value?.Set(context, ResolveTargetElement(context).InputGetValue());
        }
    }
}
