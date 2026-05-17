using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    public enum BrowserSwitchTabByType
    {
        Index,
        Title,
        TitleRegex,
        Url,
        UrlRegex,
        Tab
    }

    [DisplayName("Switch Tab")]
    [Description("Switch to a target tab in the browser by condition.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class BrowserSwitchTabActivity : CodeActivity
    {
        [DisplayName("Input Browser")]
        [Description("Browser instance where tab switching is performed.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwBrowser> Browser { get; set; }

        [DisplayName("Index")]
        [Description("Tab index used when switching by index (0-based).")]
        [Category("Input")]
        public InArgument<int?> Index { get; set; }

        [DisplayName("Title")]
        [Description("Exact title text used when switching by title.")]
        [Category("Input")]
        public InArgument<string> Title { get; set; }

        [DisplayName("Title Re")]
        [Description("Regular expression used when switching by title.")]
        [Category("Input")]
        public InArgument<string> TitleRe { get; set; }

        [DisplayName("Url")]
        [Description("Exact URL used when switching by URL.")]
        [Category("Input")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Url Re")]
        [Description("Regular expression used when switching by URL.")]
        [Category("Input")]
        public InArgument<string> UrlRe { get; set; }

        [DisplayName("Input Tab")]
        [Description("Target tab used when switching by tab object.")]
        [Category("Input")]
        public InArgument<PwTab> InputTab { get; set; }

        [DisplayName("By Type")]
        [Description("Matching mode used to switch tabs.")]
        [Category("Input")]
        [DefaultValue(BrowserSwitchTabByType.Index)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.BrowserSwitchTabByTypeTypeConverter, F2B.Browser.Chromium.Playwright")]
        public BrowserSwitchTabByType ByType
        {
            get => _byType;
            set
            {
                _byType = value;
                TypeDescriptor.Refresh(this);
            }
        }

        [DisplayName("Tab")]
        [Description("Outputs the switched tab instance.")]
        [Category("Output")]
        public OutArgument<PwTab> Tab { get; set; }

        [DisplayName("Tab Info")]
        [Description("Outputs information of the switched tab.")]
        [Category("Output")]
        public OutArgument<TabInfo> TabInfo { get; set; }

        private BrowserSwitchTabByType _byType = BrowserSwitchTabByType.Index;

        protected override void Execute(CodeActivityContext context)
        {
            int? index = null;
            string title = null;
            string titleRe = null;
            string url = null;
            string urlRe = null;
            PwTab inputTab = null;

            switch (ByType)
            {
                case BrowserSwitchTabByType.Index:
                    index = Index == null ? null : Index.Get(context);
                    break;
                case BrowserSwitchTabByType.Title:
                    title = Title == null ? null : Title.Get(context);
                    break;
                case BrowserSwitchTabByType.TitleRegex:
                    titleRe = TitleRe == null ? null : TitleRe.Get(context);
                    break;
                case BrowserSwitchTabByType.Url:
                    url = Url == null ? null : Url.Get(context);
                    break;
                case BrowserSwitchTabByType.UrlRegex:
                    urlRe = UrlRe == null ? null : UrlRe.Get(context);
                    break;
                case BrowserSwitchTabByType.Tab:
                    inputTab = InputTab == null ? null : InputTab.Get(context);
                    break;
            }

            var tab = Browser.Get(context).SwitchTab(
                index: index,
                title: title,
                titleRe: titleRe,
                url: url,
                urlRe: urlRe,
                tab: inputTab);
            Tab?.Set(context, tab);
            TabInfo?.Set(context, tab?.GetInfo());
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            switch (ByType)
            {
                case BrowserSwitchTabByType.Index:
                    if (Index == null || Index.Expression == null)
                    {
                        metadata.AddValidationError("Index must be provided when ByType=Index.");
                    }

                    break;
                case BrowserSwitchTabByType.Title:
                    if (Title == null || Title.Expression == null)
                    {
                        metadata.AddValidationError("Title must be provided when ByType=Title.");
                    }

                    break;
                case BrowserSwitchTabByType.TitleRegex:
                    if (TitleRe == null || TitleRe.Expression == null)
                    {
                        metadata.AddValidationError("TitleRe must be provided when ByType=Title Regex.");
                    }

                    break;
                case BrowserSwitchTabByType.Url:
                    if (Url == null || Url.Expression == null)
                    {
                        metadata.AddValidationError("Url must be provided when ByType=Url.");
                    }

                    break;
                case BrowserSwitchTabByType.UrlRegex:
                    if (UrlRe == null || UrlRe.Expression == null)
                    {
                        metadata.AddValidationError("UrlRe must be provided when ByType=Url Regex.");
                    }

                    break;
                case BrowserSwitchTabByType.Tab:
                    if (InputTab == null || InputTab.Expression == null)
                    {
                        metadata.AddValidationError("InputTab must be provided when ByType=Tab.");
                    }

                    break;
                default:
                    metadata.AddValidationError("Unsupported ByType.");
                    break;
            }
        }
    }
}
