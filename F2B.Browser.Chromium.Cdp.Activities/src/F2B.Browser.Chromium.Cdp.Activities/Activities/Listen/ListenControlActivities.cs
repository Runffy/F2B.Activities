using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Listen-Pause")]
    [Description("Pause capturing. ExtraInfo events keep running. Connection stays open.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class ListenPauseActivity : CodeActivity
    {
        public ListenPauseActivity()
        {
            DisplayName = "Listen-Pause";
        }

        [DisplayName("Target")]
        [Category("Input.A")]
        [RequiredArgument]
        public InArgument<CdpBase> Target { get; set; }

        [DisplayName("Clear")]
        [Description("When true, also clears captured packets. Default true.")]
        [Category("Input.B")]
        [DefaultValue(true)]
        public InArgument<bool> Clear { get; set; } = true;

        protected override void Execute(CodeActivityContext context)
        {
            var listener = CdpListenActivityHelper.GetListener(Target, context);
            var clear = CdpActivityArgumentHelper.GetOrDefault(Clear, context, true);
            listener.Pause(clear);
        }
    }

    [DisplayName("Listen-Resume")]
    [Description("Resume after Pause. Throws if listen was Stopped or never started.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class ListenResumeActivity : CodeActivity
    {
        public ListenResumeActivity()
        {
            DisplayName = "Listen-Resume";
        }

        [DisplayName("Target")]
        [Category("Input.A")]
        [RequiredArgument]
        public InArgument<CdpBase> Target { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            CdpListenActivityHelper.GetListener(Target, context).Resume();
        }
    }

    [DisplayName("Listen-Stop")]
    [Description("Stop listening, clear the queue, and close the dedicated CDP connection.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class ListenStopActivity : CodeActivity
    {
        public ListenStopActivity()
        {
            DisplayName = "Listen-Stop";
        }

        [DisplayName("Target")]
        [Category("Input.A")]
        [RequiredArgument]
        public InArgument<CdpBase> Target { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            CdpListenActivityHelper.GetListener(Target, context).Stop();
        }
    }

    [DisplayName("Listen-Clear")]
    [Description("Clear captured packets without pausing listen.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class ListenClearActivity : CodeActivity
    {
        public ListenClearActivity()
        {
            DisplayName = "Listen-Clear";
        }

        [DisplayName("Target")]
        [Category("Input.A")]
        [RequiredArgument]
        public InArgument<CdpBase> Target { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            CdpListenActivityHelper.GetListener(Target, context).Clear();
        }
    }
}
