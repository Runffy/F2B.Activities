using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
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

    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [DisplayName("Browser.SwitchTab")]
    public sealed class BrowserSwitchTabActivity : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwBrowser> Browser { get; set; }

        [Category("Input")]
        public InArgument<int?> Index { get; set; }

        [Category("Input")]
        public InArgument<string> Title { get; set; }

        [Category("Input")]
        public InArgument<string> TitleRe { get; set; }

        [Category("Input")]
        public InArgument<string> Url { get; set; }

        [Category("Input")]
        public InArgument<string> UrlRe { get; set; }

        [Category("Input")]
        public InArgument<PwTab> InputTab { get; set; }

        [Category("Input")]
        [DefaultValue(BrowserSwitchTabByType.Index)]
        [TypeConverter("Playwright.Activities.BrowserSwitchTabByTypeTypeConverter, Playwright.Activities")]
        public BrowserSwitchTabByType ByType
        {
            get => _byType;
            set
            {
                _byType = value;
                TypeDescriptor.Refresh(this);
            }
        }

        [Category("Output")]
        public OutArgument<PwTab> Tab { get; set; }

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
                        metadata.AddValidationError("ByType=Index 时必须填写 Index。");
                    }

                    break;
                case BrowserSwitchTabByType.Title:
                    if (Title == null || Title.Expression == null)
                    {
                        metadata.AddValidationError("ByType=Title 时必须填写 Title。");
                    }

                    break;
                case BrowserSwitchTabByType.TitleRegex:
                    if (TitleRe == null || TitleRe.Expression == null)
                    {
                        metadata.AddValidationError("ByType=Title Regex 时必须填写 TitleRe。");
                    }

                    break;
                case BrowserSwitchTabByType.Url:
                    if (Url == null || Url.Expression == null)
                    {
                        metadata.AddValidationError("ByType=Url 时必须填写 Url。");
                    }

                    break;
                case BrowserSwitchTabByType.UrlRegex:
                    if (UrlRe == null || UrlRe.Expression == null)
                    {
                        metadata.AddValidationError("ByType=Url Regex 时必须填写 UrlRe。");
                    }

                    break;
                case BrowserSwitchTabByType.Tab:
                    if (InputTab == null || InputTab.Expression == null)
                    {
                        metadata.AddValidationError("ByType=Tab 时必须填写 InputTab。");
                    }

                    break;
                default:
                    metadata.AddValidationError("不支持的 ByType。");
                    break;
            }
        }
    }
}
