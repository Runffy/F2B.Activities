using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    public enum GetCookiesBaseOn
    {
        Tab,
        Browser
    }

    [DisplayName("Get Cookies")]
    [Description("Read cookies from a browser or tab.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class GetCookiesActivity : CodeActivity
    {
        public GetCookiesActivity()
        {
            DisplayName = "Get Cookies";
        }

        [DisplayName("Base On")]
        [Description("Choose whether to read cookies from browser or tab.")]
        [Category("Input")]
        [DefaultValue(GetCookiesBaseOn.Tab)]
        public GetCookiesBaseOn BaseOn { get; set; } = GetCookiesBaseOn.Tab;

        [DisplayName("Input Browser")]
        [Description("Browser instance used to read cookies.")]
        [Category("Input")]
        public InArgument<PwBrowser> InputBrowser { get; set; }

        [DisplayName("Input Tab")]
        [Description("Tab instance used to read cookies.")]
        [Category("Input")]
        public InArgument<PwTab> InputTab { get; set; }

        [DisplayName("Cookies")]
        [Description("Outputs the retrieved cookie data.")]
        [Category("Output")]
        public OutArgument<Cookies> Cookies { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Cookies result;
            switch (BaseOn)
            {
                case GetCookiesBaseOn.Browser:
                    var browser = InputBrowser == null ? null : InputBrowser.Get(context);
                    if (browser == null)
                    {
                        throw new InvalidOperationException("InputBrowser must be provided when BaseOn=Browser.");
                    }

                    result = browser.GetCookies();
                    break;
                default:
                    var tab = InputTab == null ? null : InputTab.Get(context);
                    if (tab == null)
                    {
                        throw new InvalidOperationException("InputTab must be provided when BaseOn=Tab.");
                    }

                    result = tab.GetCookies();
                    break;
            }

            Cookies?.Set(context, result);
        }
    }
}
