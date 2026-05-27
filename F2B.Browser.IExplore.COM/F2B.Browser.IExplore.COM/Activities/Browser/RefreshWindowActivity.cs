using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Refresh")]
    [Description("Refresh current embedded IE document and wait ready.")]
    [Designer(typeof(RequiredFieldsActivityDesigner))]
    public sealed class RefreshWindowActivity : CodeActivity
    {
        [Category("Input")]
        [DisplayName("Input Window")]
        [RequiredArgument]
        public InArgument<IEWindowController> InputWindow { get; set; }

        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Time")]
        [DisplayName("Timeout")]
        [DefaultValue(60000)]
        public InArgument<int> Timeout { get; set; } = 60000;

        [Category("Time")]
        [DisplayName("Interval")]
        [DefaultValue(200)]
        public InArgument<int> Interval { get; set; } = 200;

        [Category("Output")]
        [DisplayName("Window")]
        public OutArgument<IEWindowController> Window { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context, 300);

            var window = InputWindow == null ? null : InputWindow.Get(context);
            if (window == null)
            {
                throw new ArgumentException("InputWindow is required.");
            }

            var result = window.refresh(
                timeout: ActivityArgumentHelper.GetOrDefault(Timeout, context, 60000),
                interval: ActivityArgumentHelper.GetOrDefault(Interval, context, 200));

            Window.Set(context, result);
        }
    }
}
