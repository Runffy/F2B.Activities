using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Uncheck")]
    [Description("Uncheck the target element.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class UncheckActivity : ElementTargetActivityBase
    {
        public UncheckActivity() : base("Uncheck") {}

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElementWithTimeout(context, Timeout).Uncheck();
        }
    }
}
