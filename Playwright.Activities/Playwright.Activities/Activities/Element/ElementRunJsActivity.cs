using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.RunJs")]
    public sealed class ElementRunJsActivity : ElementTargetActivityBase
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Script { get; set; }

        [Category("Input")]
        public InArgument<object> Arg { get; set; }

        [Category("Output")]
        public OutArgument<object> Result { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var result = ResolveTargetElement(context).RunJs<object>(
                Script.Get(context),
                Arg == null ? null : Arg.Get(context));
            Result?.Set(context, result);
        }
    }
}
