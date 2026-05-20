using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Is Checked")]
    [Description("Returns whether a checkbox/radio is checked.")]
    public sealed class IsCheckedActivity : ElementTargetActivityBase
    {
        [DisplayName("Is Checked")]
        [Category("Output")]
        public OutArgument<bool> Result { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var window = GetWindow(context);
            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);
            bool value;

            if (TargetType == IeElementTargetType.Element)
                value = window.IsChecked(ResolveTargetElement(context), timeout);
            else
                value = window.IsChecked(ResolveLocator(context), timeout);

            Result?.Set(context, value);
        }
    }
}
