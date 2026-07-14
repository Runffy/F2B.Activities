using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Tab-Navigate")]
    [Description("Navigate, go back, go forward, or refresh the tab, then optionally wait for a ready state.")]
    [Designer(typeof(CdpCanvasFieldsActivityDesigner))]
    public sealed class TabNavigateActivity : CodeActivity
    {
        public TabNavigateActivity()
        {
            DisplayName = "Tab-Navigate";
        }

        [DisplayName("Input Tab")]
        [Description("Tab to navigate. Optional when Selector contains <wnd>.")]
        [Category("Input.A")]
        public InArgument<CdpTab> Tab { get; set; }

        [DisplayName("Selector")]
        [Description("Optional window selector XML containing <wnd>. Use instead of Input Tab.")]
        [Category("Input.A")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Type")]
        [Description("Navigation action type.")]
        [Category("Input.B")]
        [DefaultValue(CdpNavigateType.Navigate)]
        [TypeConverter(typeof(CdpNavigateTypeConverter))]
        public CdpNavigateType Type { get; set; } = CdpNavigateType.Navigate;

        [DisplayName("Url")]
        [Description("Destination URL. Used only when Type is Navigate.")]
        [Category("Input.C")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Wait For State")]
        [Description("Wait until the tab reaches this ready state after navigation.")]
        [Category("Input.D")]
        [DefaultValue(CdpWaitForState.Complete)]
        [TypeConverter(typeof(CdpWaitForStateConverter))]
        public CdpWaitForState WaitForState { get; set; } = CdpWaitForState.Complete;

        [DisplayName("Timeout (ms)")]
        [Description("Maximum wait time for the ready state.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var selector = Selector == null ? null : Selector.Get(context);
            var inputTab = Tab == null ? null : Tab.Get(context);
            var tab = CdpTabLocator.Resolve(selector, inputTab);
            var timeoutMs = CdpActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);

            switch (Type)
            {
                case CdpNavigateType.Back:
                    tab.Back();
                    break;
                case CdpNavigateType.Forward:
                    tab.Forward();
                    break;
                case CdpNavigateType.Refresh:
                    tab.Refresh();
                    break;
                default:
                    var url = Url == null ? null : Url.Get(context);
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        throw new System.InvalidOperationException("Url is required when Type is Navigate.");
                    }

                    tab.Navigate(url);
                    break;
            }

            CdpTabWaitHelper.WaitForReadyState(tab, WaitForState, timeoutMs);
        }
    }
}
