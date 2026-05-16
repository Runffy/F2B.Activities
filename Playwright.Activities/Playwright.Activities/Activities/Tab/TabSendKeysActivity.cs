using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [DisplayName("Tab.SendKeys")]
    public sealed class TabSendKeysActivity : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwTab> Tab { get; set; }

        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Keys { get; set; }

        [Category("Input")]
        public InArgument<int?> Delay { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tab.Get(context).SendKeys(
                keys: Keys.Get(context),
                delay: Delay == null ? null : Delay.Get(context));
        }
    }
}
