using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Find IExplore Window")]
    [Description("Connect to an embedded IE window by title/title regex/hwnd.")]
    [Designer(typeof(RequiredFieldsActivityDesigner))]
    public sealed class ConnectEmbeddedIeWindowActivity : CodeActivity
    {
        [Category("Filter")]
        [DisplayName("Title")]
        public InArgument<string> Title { get; set; }

        [Category("Filter")]
        [DisplayName("Title Regex")]
        public InArgument<string> TitleRegex { get; set; }
        
        [Category("Filter")]
        [DisplayName("HWND")]
        public InArgument<long?> Hwnd { get; set; }

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
        [DefaultValue(500)]
        public InArgument<int> Interval { get; set; } = 500;
        
        [Category("Output")]
        [DisplayName("Window")]
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
