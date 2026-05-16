using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.Input")]
    public sealed class ElementInputActivity : ElementTargetActivityBase
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Value { get; set; }

        [Category("Input")]
        [DefaultValue(Playwright.Activities.InputMethod.Fill)]
        [TypeConverter("Playwright.Activities.InputMethodTypeConverter, Playwright.Activities")]
        public InputMethod InputMethod { get; set; } = Playwright.Activities.InputMethod.Fill;

        [Category("Input")]
        public InArgument<float?> TypeDelay { get; set; }

        [Category("Input")]
        [DefaultValue(false)]
        [TypeConverter("Playwright.Activities.BooleanTypeConverter, Playwright.Activities")]
        public bool ValidateContentAfterInputted { get; set; } = false;

        [Category("Input")]
        [DefaultValue(500)]
        public InArgument<int> Interval { get; set; } = 500;

        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElement(context).Input(
                value: Value.Get(context),
                inputMethod: InputMethod,
                typeDelay: TypeDelay == null ? null : TypeDelay.Get(context),
                validateContentAfterInputted: ValidateContentAfterInputted,
                interval: ActivityArgumentHelper.GetOrDefault(Interval, context, 500));
        }
    }
}
