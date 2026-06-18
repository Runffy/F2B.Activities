using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Get Cookies")]
    [Description("Read cookies from a browser or tab.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class GetCookiesActivity : CodeActivity
    {
        public GetCookiesActivity()
        {
            DisplayName = "Get Cookies";
        }

        [DisplayName("Base On")]
        [Category("Input")]
        [DefaultValue(BridgeGetCookiesBaseOn.Tab)]
        public BridgeGetCookiesBaseOn BaseOn { get; set; } = BridgeGetCookiesBaseOn.Tab;

        [DisplayName("Input Browser")]
        [Category("Input")]
        public InArgument<BwBrowser> InputBrowser { get; set; }

        [DisplayName("Input Tab")]
        [Category("Input")]
        public InArgument<BwTab> InputTab { get; set; }

        [DisplayName("Cookies")]
        [Category("Output")]
        public OutArgument<BwCookie[]> Cookies { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            BwCookie[] result;
            if (BaseOn == BridgeGetCookiesBaseOn.Browser)
            {
                var browser = InputBrowser == null ? null : InputBrowser.Get(context);
                if (browser == null)
                    throw new InvalidOperationException("InputBrowser must be provided when BaseOn=Browser.");
                result = browser.GetCookies();
            }
            else
            {
                var tab = InputTab == null ? null : InputTab.Get(context);
                if (tab == null)
                    throw new InvalidOperationException("InputTab must be provided when BaseOn=Tab.");
                result = tab.GetCookies();
            }

            Cookies?.Set(context, result);
        }
    }
}
