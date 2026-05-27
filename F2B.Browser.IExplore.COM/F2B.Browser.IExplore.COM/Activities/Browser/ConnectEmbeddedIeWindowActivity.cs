using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Find IExplore Window")]
    [Description("Connect to an embedded IE window by title/title regex/hwnd.")]
    [Designer(typeof(RequiredFieldsActivityDesigner))]
    public sealed class ConnectEmbeddedIeWindowActivity : CodeActivity
    {
        [DisplayName("Title")]
        [Category("Filter")]
        public InArgument<string> Title { get; set; }

        [DisplayName("Title Regex")]
        [Category("Filter")]
        public InArgument<string> TitleRegex { get; set; }

        [DisplayName("HWND")]
        [Category("Filter")]
        public InArgument<long?> Hwnd { get; set; }

        [DisplayName("Delay Before")]
        [Category("Time")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [DisplayName("Timeout")]
        [Category("Time")]
        [DefaultValue(60000)]
        public InArgument<int> Timeout { get; set; } = 60000;

        [DisplayName("Interval")]
        [Category("Time")]
        [DefaultValue(500)]
        public InArgument<int> Interval { get; set; } = 500;

        [DisplayName("Window")]
        [Category("Output")]
        public OutArgument<IEWindowController> Window { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context, 300);

            var result = IEWindowController.connect_embedded_ie_window(
                title: Title == null ? null : Title.Get(context),
                title_re: TitleRegex == null ? null : TitleRegex.Get(context),
                hwnd: Hwnd == null ? null : Hwnd.Get(context),
                timeout: ActivityArgumentHelper.GetOrDefault(Timeout, context, 60000),
                interval: ActivityArgumentHelper.GetOrDefault(Interval, context, 500));

            Window.Set(context, result);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            var hasTitle = Title != null && Title.Expression != null;
            var hasTitleRegex = TitleRegex != null && TitleRegex.Expression != null;
            var hasHwnd = Hwnd != null && Hwnd.Expression != null;

            if (!hasTitle && !hasTitleRegex && !hasHwnd)
            {
                metadata.AddValidationError("At least one filter is required: Title, Title Regex, or HWND.");
            }
        }
    }
}
