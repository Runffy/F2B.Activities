using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Switch Tab")]
    [Description("Switch to a target tab in the browser by condition.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class BrowserSwitchTabActivity : CodeActivity
    {
        public BrowserSwitchTabActivity()
        {
            DisplayName = "Switch Tab";
        }

        [DisplayName("Input Browser")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<BwBrowser> Browser { get; set; }

        [DisplayName("By Type")]
        [Category("Input.B")]
        [DefaultValue(BridgeSwitchTabByType.Index)]
        [TypeConverter("F2B.Browser.Chromium.Bridge.BridgeSwitchTabByTypeConverter, F2B.Browser.Chromium.Bridge")]
        public BridgeSwitchTabByType ByType
        {
            get => _byType;
            set
            {
                _byType = value;
                TypeDescriptor.Refresh(this);
            }
        }

        [DisplayName("Index")]
        [Category("Input.C")]
        public InArgument<int?> Index { get; set; }

        [DisplayName("Title")]
        [Category("Input.C")]
        public InArgument<string> Title { get; set; }

        [DisplayName("Title Regex")]
        [Category("Input.C")]
        public InArgument<string> TitleRe { get; set; }

        [DisplayName("Url")]
        [Category("Input.C")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Url Regex")]
        [Category("Input.C")]
        public InArgument<string> UrlRe { get; set; }

        [DisplayName("Input Tab")]
        [Category("Input.C")]
        public InArgument<BwTab> InputTab { get; set; }

        [DisplayName("Tab")]
        [Category("Output")]
        public OutArgument<BwTab> Tab { get; set; }

        [DisplayName("Tab Info")]
        [Category("Output")]
        public OutArgument<BwTabInfo> TabInfo { get; set; }

        private BridgeSwitchTabByType _byType = BridgeSwitchTabByType.Index;

        protected override void Execute(CodeActivityContext context)
        {
            int? index = null;
            string title = null;
            string titleRe = null;
            string url = null;
            string urlRe = null;
            BwTab inputTab = null;

            switch (ByType)
            {
                case BridgeSwitchTabByType.Index:
                    index = Index == null ? null : Index.Get(context);
                    break;
                case BridgeSwitchTabByType.Title:
                    title = Title == null ? null : Title.Get(context);
                    break;
                case BridgeSwitchTabByType.TitleRegex:
                    titleRe = TitleRe == null ? null : TitleRe.Get(context);
                    break;
                case BridgeSwitchTabByType.Url:
                    url = Url == null ? null : Url.Get(context);
                    break;
                case BridgeSwitchTabByType.UrlRegex:
                    urlRe = UrlRe == null ? null : UrlRe.Get(context);
                    break;
                case BridgeSwitchTabByType.Tab:
                    inputTab = InputTab == null ? null : InputTab.Get(context);
                    break;
            }

            var tab = Browser.Get(context).SwitchTab(index, title, titleRe, url, urlRe, inputTab);
            Tab?.Set(context, tab);
            TabInfo?.Set(context, tab.GetInfo());
        }
    }
}
