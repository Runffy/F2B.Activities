using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    public enum GetStorageScope
    {
        Session,
        Local
    }

    [DisplayName("Get Storage")]
    [Description("Read storage data from a browser or tab.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class GetStorageActivity : CodeActivity
    {
        public GetStorageActivity()
        {
            DisplayName = "Get Storage";
        }

        [DisplayName("Base On")]
        [Description("Choose whether to read storage from browser or tab.")]
        [Category("Input.A")]
        [DefaultValue(GetCookiesBaseOn.Tab)]
        public GetCookiesBaseOn BaseOn { get; set; } = GetCookiesBaseOn.Tab;

        [DisplayName("Scope")]
        [Description("Specify SessionStorage or LocalStorage.")]
        [Category("Input.C")]
        [DefaultValue(GetStorageScope.Session)]
        public GetStorageScope Scope { get; set; } = GetStorageScope.Session;

        [DisplayName("Input Browser")]
        [Description("Browser instance used to read storage.")]
        [Category("Input.B")]
        public InArgument<PwBrowser> InputBrowser { get; set; }

        [DisplayName("Input Tab")]
        [Description("Tab instance used to read storage.")]
        [Category("Input.B")]
        public InArgument<PwTab> InputTab { get; set; }

        [DisplayName("Storage")]
        [Description("Outputs the retrieved storage data.")]
        [Category("Output")]
        public OutArgument<Storages> Storage { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Storages result;
            switch (BaseOn)
            {
                case GetCookiesBaseOn.Browser:
                    var browser = InputBrowser == null ? null : InputBrowser.Get(context);
                    if (browser == null)
                    {
                        throw new InvalidOperationException("InputBrowser must be provided when BaseOn=Browser.");
                    }

                    result = Scope == GetStorageScope.Local ? browser.GetLocalStorage() : browser.GetSessionStorage();
                    break;
                default:
                    var tab = InputTab == null ? null : InputTab.Get(context);
                    if (tab == null)
                    {
                        throw new InvalidOperationException("InputTab must be provided when BaseOn=Tab.");
                    }

                    result = Scope == GetStorageScope.Local ? tab.GetLocalStorage() : tab.GetSessionStorage();
                    break;
            }

            Storage?.Set(context, result);
        }
    }
}
