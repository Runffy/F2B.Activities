using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Element Exists")]
    [Description("Instantly check whether selector exists in current frame/document.")]
    public sealed class ElementExistsActivity : IeElementActivityBase
    {
        public ElementExistsActivity()
        {
            Timeout = 0;
        }

        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Output")]
        [DisplayName("Exists")]
        public OutArgument<bool> Exists { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context, 300);

            try
            {
                var elements = ResolveWindow(context).get_elements(
                    locator: ResolveSelector(context),
                    frame_path: ResolveFramePath(context));

                Exists.Set(context, elements != null && elements.Length > 0);
            }
            catch
            {
                Exists.Set(context, false);
            }
        }
    }
}
