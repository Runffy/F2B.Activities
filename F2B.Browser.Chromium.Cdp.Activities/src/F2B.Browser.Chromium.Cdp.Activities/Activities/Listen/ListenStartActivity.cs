using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [ToolboxPath("Listen")]
    [DisplayName("Start")]
    [Description("Start listening for network packets on a Tab or Frame. Clears previously captured packets.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class ListenStartActivity : CodeActivity
    {
        public ListenStartActivity()
        {
            DisplayName = "Start";
            Method = CdpNetworkMethod.Get | CdpNetworkMethod.Post;
            ResourceType = CdpNetworkResourceType.All;
        }


        [DisplayName("Target")]
        [Description("CdpTab or CdpFrame to listen on.")]
        [Category("Input.A")]
        [RequiredArgument]
        public InArgument<CdpBase> Target { get; set; }


        [DisplayName("Targets")]
        [Description("URL patterns to match. Null or empty matches all URLs.")]
        [Category("Input.B")]
        public InArgument<string[]> Targets { get; set; }


        [DisplayName("Is Regex")]
        [Description("When true, each Targets item is treated as a regular expression.")]
        [Category("Input.B")]
        [DefaultValue(false)]
        public InArgument<bool> IsRegex { get; set; } = false;


        [DisplayName("Method")]
        [Description("HTTP methods to capture. Default GET|POST. Use All for every method.")]
        [Category("Input.C")]
        [DefaultValue(CdpNetworkMethod.Get | CdpNetworkMethod.Post)]
        [TypeConverter(typeof(CdpNetworkMethodConverter))]
        public CdpNetworkMethod Method { get; set; }


        [DisplayName("Resource Type")]
        [Description("CDP resource types to capture. Default All.")]
        [Category("Input.C")]
        [DefaultValue(CdpNetworkResourceType.All)]
        [TypeConverter(typeof(CdpNetworkResourceTypeConverter))]
        public CdpNetworkResourceType ResourceType { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var listener = CdpListenActivityHelper.GetListener(Target, context);
            var targets = Targets == null ? null : Targets.Get(context);
            var isRegex = CdpActivityArgumentHelper.GetOrDefault(IsRegex, context, false);
            listener.Start(targets, isRegex, Method, ResourceType);
        }
    }
}
