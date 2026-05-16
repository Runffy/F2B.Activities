using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [DisplayName("Tab.RunJs")]
    public sealed class TabRunJsActivity : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwTab> Tab { get; set; }

        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Script { get; set; }

        [Category("Input")]
        public InArgument<object> Arg { get; set; }

        [Category("Output")]
        public OutArgument<object> Result { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var result = Tab.Get(context).RunJs<object>(
                Script.Get(context),
                Arg == null ? null : Arg.Get(context));
            Result?.Set(context, result);
        }
    }
}
