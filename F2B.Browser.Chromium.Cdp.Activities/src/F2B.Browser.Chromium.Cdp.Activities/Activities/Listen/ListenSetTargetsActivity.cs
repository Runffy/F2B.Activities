using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [ToolboxPath("Listen")]
    [DisplayName("SetTargets")]
    [Description("Change listen URL/method/resource filters without clearing the captured queue.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class ListenSetTargetsActivity : CodeActivity
    {
        public ListenSetTargetsActivity()
        {
            DisplayName = "SetTargets";
            Method = CdpNetworkMethod.Get | CdpNetworkMethod.Post;
            ResourceType = CdpNetworkResourceType.All;
        }


        [DisplayName("Target")]
        [Description("CdpTab or CdpFrame.")]
        [Category("Input.A")]
        [RequiredArgument]
        public InArgument<CdpBase> Target { get; set; }


        [DisplayName("Targets")]
        [Description("URL patterns to match. Null or empty matches all URLs.")]
        [Category("Input.B")]
        public InArgument<string[]> Targets { get; set; }


        [DisplayName("Is Regex")]
        [Category("Input.B")]
        [DefaultValue(false)]
        public InArgument<bool> IsRegex { get; set; } = false;


        [DisplayName("Method")]
        [Category("Input.C")]
        [DefaultValue(CdpNetworkMethod.Get | CdpNetworkMethod.Post)]
        [TypeConverter(typeof(CdpNetworkMethodConverter))]
        public CdpNetworkMethod Method { get; set; }


        [DisplayName("Resource Type")]
        [Category("Input.C")]
        [DefaultValue(CdpNetworkResourceType.All)]
        [TypeConverter(typeof(CdpNetworkResourceTypeConverter))]
        public CdpNetworkResourceType ResourceType { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var listener = CdpListenActivityHelper.GetListener(Target, context);
            var targets = Targets == null ? null : Targets.Get(context);
            var isRegex = CdpActivityArgumentHelper.GetOrDefault(IsRegex, context, false);
            listener.SetTargets(targets, isRegex, Method, ResourceType);
        }
    }
}
