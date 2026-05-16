using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.SendKeys")]
    public sealed class ElementSendKeysActivity : ElementTargetActivityBase
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Keys { get; set; }

        [Category("Input")]
        public InArgument<int?> Delay { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElement(context).SendKeys(
                keys: Keys.Get(context),
                delay: Delay == null ? null : Delay.Get(context));
        }
    }
}
