using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Tab-GetRequest")]
    [Description("Send a GET request in the tab context.")]
    [Designer(typeof(CdpCanvasFieldsActivityDesigner))]
    public sealed class TabGetRequestActivity : CodeActivity
    {
        public TabGetRequestActivity()
        {
            DisplayName = "Tab-GetRequest";
        }

        [DisplayName("Tab")]
        [Description("Tab instance. Optional when Selector contains <wnd>.")]
        [Category("Input.A")]
        public InArgument<CdpTab> Tab { get; set; }

        [DisplayName("Selector")]
        [Description("Selector XML with <wnd> used to locate the tab.")]
        [Category("Input.A")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Url")]
        [Description("Request URL.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Request timeout in milliseconds.")]
        [Category("Input.C")]
        [DefaultValue(30000)]
        public InArgument<int> Timeout { get; set; } = 30000;

        [DisplayName("Certifications")]
        [Description("Client certificate file paths (reserved for future support).")]
        [Category("Input.C")]
        public InArgument<string[]> Certifications { get; set; }

        [DisplayName("Response")]
        [Description("Outputs the HTTP response.")]
        [Category("Output")]
        public OutArgument<CdpResponse> Response { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var selector = Selector == null ? null : Selector.Get(context);
            var tab = Tab == null ? null : Tab.Get(context);
            var resolvedTab = CdpTabLocator.Resolve(selector, tab);
            var url = Url.Get(context);
            var timeoutMs = CdpActivityArgumentHelper.GetOrDefault(Timeout, context, 30000);
            var certifications = Certifications == null ? null : Certifications.Get(context);

            var response = CdpHttpRequestHelper.Get(resolvedTab, url, timeoutMs, certifications);
            Response?.Set(context, response);
        }
    }
}
