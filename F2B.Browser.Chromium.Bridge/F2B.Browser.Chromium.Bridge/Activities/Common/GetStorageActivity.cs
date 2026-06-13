using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Get Storage")]
    [Description("Read storage data from a browser or tab.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class GetStorageActivity : CodeActivity
    {
        public GetStorageActivity()
        {
            DisplayName = "Get Storage";
        }

        [DisplayName("Base On")]
        [Category("Input.A")]
        [DefaultValue(BridgeGetCookiesBaseOn.Tab)]
        public BridgeGetCookiesBaseOn BaseOn { get; set; } = BridgeGetCookiesBaseOn.Tab;

        [DisplayName("Scope")]
        [Category("Input.C")]
        [DefaultValue(BridgeStorageScope.Session)]
        public BridgeStorageScope Scope { get; set; } = BridgeStorageScope.Session;

        [DisplayName("Input Browser")]
        [Category("Input.B")]
        public InArgument<BwBrowser> InputBrowser { get; set; }

        [DisplayName("Input Tab")]
        [Category("Input.B")]
        public InArgument<BwTab> InputTab { get; set; }

        [DisplayName("Storage")]
        [Category("Output")]
        public OutArgument<Dictionary<string, string>> Storage { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Dictionary<string, string> result;
            if (BaseOn == BridgeGetCookiesBaseOn.Browser)
            {
                var browser = InputBrowser == null ? null : InputBrowser.Get(context);
                if (browser == null)
                    throw new InvalidOperationException("InputBrowser must be provided when BaseOn=Browser.");
                result = Scope == BridgeStorageScope.Local
                    ? browser.GetLocalStorage()
                    : browser.GetSessionStorage();
            }
            else
            {
                var tab = InputTab == null ? null : InputTab.Get(context);
                if (tab == null)
                    throw new InvalidOperationException("InputTab must be provided when BaseOn=Tab.");
                result = Scope == BridgeStorageScope.Local
                    ? tab.GetLocalStorage()
                    : tab.GetSessionStorage();
            }

            Storage?.Set(context, result);
        }
    }
}
