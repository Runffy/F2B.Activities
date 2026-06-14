using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Get Browser Status")]
    [Description("Get the activated tab and page load state for diagnostics (loading, complete, error page, title, url).")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class BrowserGetStatusActivity : CodeActivity
    {
        public BrowserGetStatusActivity()
        {
            DisplayName = "Get Browser Status";
        }

        [DisplayName("Input Browser")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<BwBrowser> Browser { get; set; }

        [DisplayName("Status")]
        [Description("Full browser/tab status snapshot.")]
        [Category("Output")]
        public OutArgument<BwBrowserStatus> Status { get; set; }

        [DisplayName("Activated Tab")]
        [Category("Output")]
        public OutArgument<BwTab> ActivatedTab { get; set; }

        [DisplayName("Title")]
        [Category("Output")]
        public OutArgument<string> Title { get; set; }

        [DisplayName("Url")]
        [Category("Output")]
        public OutArgument<string> Url { get; set; }

        [DisplayName("Is Loading")]
        [Category("Output")]
        public OutArgument<bool> IsLoading { get; set; }

        [DisplayName("Is Load Complete")]
        [Category("Output")]
        public OutArgument<bool> IsLoadComplete { get; set; }

        [DisplayName("Is Error Page")]
        [Category("Output")]
        public OutArgument<bool> IsErrorPage { get; set; }

        [DisplayName("Is Likely Broken Page")]
        [Description("True when load is complete but the page is blank, restricted, or a browser error page.")]
        [Category("Output")]
        public OutArgument<bool> IsLikelyBrokenPage { get; set; }

        [DisplayName("Has Activated Tab")]
        [Category("Output")]
        public OutArgument<bool> HasActivatedTab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var status = Browser.Get(context).GetStatus();

            Status?.Set(context, status);
            ActivatedTab?.Set(context, status.ActivatedTab);
            Title?.Set(context, status.Title ?? string.Empty);
            Url?.Set(context, status.Url ?? string.Empty);
            IsLoading?.Set(context, status.IsLoading);
            IsLoadComplete?.Set(context, status.IsLoadComplete);
            IsErrorPage?.Set(context, status.IsErrorPage);
            IsLikelyBrokenPage?.Set(context, status.IsLikelyBrokenPage);
            HasActivatedTab?.Set(context, status.HasActivatedTab);
        }
    }
}
