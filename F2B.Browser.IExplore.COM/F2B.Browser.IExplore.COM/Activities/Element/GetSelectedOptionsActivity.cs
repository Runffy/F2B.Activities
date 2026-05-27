using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Get Selected Options")]
    [Description("Get selected option texts from dropdown element.")]
    public sealed class GetSelectedOptionsActivity : IeElementActivityBase
    {
        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Output")]
        [DisplayName("Values")]
        public OutArgument<string[]> Values { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context, 300);

            var result = ResolveWindow(context).get_selected_options(
                locator: ResolveSelector(context),
                frame_path: ResolveFramePath(context),
                timeout: ResolveTimeout(context));

            Values.Set(context, result);
        }
    }
}
