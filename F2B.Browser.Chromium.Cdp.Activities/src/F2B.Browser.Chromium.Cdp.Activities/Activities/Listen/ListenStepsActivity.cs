using System;
using System.Activities;
using System.Activities.Statements;
using System.Activities.Presentation;
using System.ComponentModel;
using System.Windows;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    internal sealed class ListenBreakException : Exception
    {
        public ListenBreakException()
            : base("Listen-Break")
        {
        }
    }

    internal sealed class ListenContinueException : Exception
    {
        public ListenContinueException()
            : base("Listen-Continue")
        {
        }
    }

    [ToolboxPath("Listen")]

    [DisplayName("Steps")]
    [Description("Loop as packets arrive. Each iteration exposes Packets. Timeout on a round ends the loop without throwing or stopping listen.")]
    [Designer(typeof(ListenStepsActivityDesigner))]
    public sealed class ListenStepsActivity : NativeActivity, IActivityTemplateFactory
    {
        public const string ControlPropertyName = "CdpListenStepsControl";
        public const string DefaultPacketsName = "Packets";

        private readonly Variable<int> _caught = new Variable<int>("ListenStepsCaught");
        private readonly Variable<int> _gap = new Variable<int>("ListenStepsGap");
        private readonly Variable<int> _timeoutMs = new Variable<int>("ListenStepsTimeout");
        private readonly Variable<int> _countLimit = new Variable<int>("ListenStepsCountLimit");

        public ListenStepsActivity()
        {
            DisplayName = "Steps";
            EnsureBody(null);
        }


        [DisplayName("Target")]
        [Description("CdpTab or CdpFrame.")]
        [Category("Input.A")]
        [RequiredArgument]
        public InArgument<CdpBase> Target { get; set; }


        [DisplayName("Count")]
        [Description("Total packets to capture. ≤0 means unlimited.")]
        [Category("Input.B")]
        [DefaultValue(0)]
        public InArgument<int> Count { get; set; } = 0;


        [DisplayName("Gap")]
        [Description("Packets yielded per iteration.")]
        [Category("Input.B")]
        [DefaultValue(1)]
        public InArgument<int> Gap { get; set; } = 1;


        [DisplayName("Timeout (ms)")]
        [Description("Wait time per round. ≤0 waits forever. Timeout ends the loop without throwing.")]
        [Category("Input.Z")]
        [DefaultValue(0)]
        public InArgument<int> Timeout { get; set; } = 0;


        [DisplayName("Packets")]
        [Description("Variable name for the current packet batch inside Body.")]
        [Category("Input.A")]
        [DefaultValue(DefaultPacketsName)]
        public string Packets
        {
            get
            {
                EnsureBody(null);
                return Body.Argument.Name;
            }
            set
            {
                EnsureBody(null);
                Body.Argument.Name = string.IsNullOrWhiteSpace(value) ? DefaultPacketsName : value.Trim();
            }
        }

        [Browsable(false)]
        public ActivityAction<CdpNetworkPacket[]> Body { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new ListenStepsActivity
            {
                DisplayName = "Steps",
                Count = new InArgument<int>(0),
                Gap = new InArgument<int>(1),
                Timeout = new InArgument<int>(0),
                Body = CreateBodyAction(new Sequence { DisplayName = "Body" }, DefaultPacketsName)
            };
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            EnsureBody(null);
            BindIn(metadata, Target, "Target", typeof(CdpBase), required: true);
            BindIn(metadata, Count, "Count", typeof(int), required: false);
            BindIn(metadata, Gap, "Gap", typeof(int), required: false);
            BindIn(metadata, Timeout, "Timeout", typeof(int), required: false);

            if (Body != null)
            {
                metadata.AddDelegate(Body);
            }

            metadata.AddImplementationVariable(_caught);
            metadata.AddImplementationVariable(_gap);
            metadata.AddImplementationVariable(_timeoutMs);
            metadata.AddImplementationVariable(_countLimit);
        }

        protected override void Execute(NativeActivityContext context)
        {
            context.Properties.Add(ControlPropertyName, new object());
            var gap = Math.Max(1, CdpActivityArgumentHelper.GetOrDefault(Gap, context, 1));
            var timeoutMs = CdpActivityArgumentHelper.GetOrDefault(Timeout, context, 0);
            var count = CdpActivityArgumentHelper.GetOrDefault(Count, context, 0);
            context.SetValue(_caught, 0);
            context.SetValue(_gap, gap);
            context.SetValue(_timeoutMs, timeoutMs);
            context.SetValue(_countLimit, count);
            ScheduleRound(context);
        }

        private void ScheduleRound(NativeActivityContext context)
        {
            var listener = CdpListenActivityHelper.GetListener(Target, context);
            var gap = context.GetValue(_gap);
            var timeoutMs = context.GetValue(_timeoutMs);
            var packets = listener.Wait(gap, timeoutMs, throwOnTimeout: false);
            if (packets == null || packets.Length == 0)
            {
                return;
            }

            var caught = context.GetValue(_caught) + packets.Length;
            context.SetValue(_caught, caught);

            if (Body == null || Body.Handler == null)
            {
                MaybeContinue(context);
                return;
            }

            context.ScheduleAction(Body, packets, OnBodyComplete, OnBodyFault);
        }

        private void OnBodyComplete(NativeActivityContext context, ActivityInstance completedInstance)
        {
            MaybeContinue(context);
        }

        private void OnBodyFault(NativeActivityFaultContext faultContext, Exception exception, ActivityInstance propagatedFrom)
        {
            if (exception is ListenBreakException)
            {
                faultContext.HandleFault();
                return;
            }

            if (exception is ListenContinueException)
            {
                faultContext.HandleFault();
                MaybeContinue(faultContext);
                return;
            }
        }

        private void MaybeContinue(NativeActivityContext context)
        {
            var limit = context.GetValue(_countLimit);
            var caught = context.GetValue(_caught);
            if (limit > 0 && caught >= limit)
            {
                return;
            }

            ScheduleRound(context);
        }

        private static void BindIn(NativeActivityMetadata metadata, Argument argument, string name, Type type, bool required)
        {
            var runtime = new RuntimeArgument(name, type, ArgumentDirection.In, required);
            metadata.Bind(argument, runtime);
            metadata.AddArgument(runtime);
        }

        private void EnsureBody(string packetsName)
        {
            var name = string.IsNullOrWhiteSpace(packetsName)
                ? (Body != null && Body.Argument != null ? Body.Argument.Name : DefaultPacketsName)
                : packetsName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = DefaultPacketsName;
            }

            if (Body == null)
            {
                Body = CreateBodyAction(new Sequence { DisplayName = "Body" }, name);
                return;
            }

            if (Body.Argument == null)
            {
                Body.Argument = new DelegateInArgument<CdpNetworkPacket[]>(name);
            }
            else if (string.IsNullOrWhiteSpace(Body.Argument.Name))
            {
                Body.Argument.Name = name;
            }
        }

        private static ActivityAction<CdpNetworkPacket[]> CreateBodyAction(Activity handler, string packetsName)
        {
            return new ActivityAction<CdpNetworkPacket[]>
            {
                Argument = new DelegateInArgument<CdpNetworkPacket[]>(packetsName),
                Handler = handler
            };
        }
    }

    [ToolboxPath("Listen")]

    [DisplayName("Break")]
    [Description("Exit Listen-Steps. Does not stop listening.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class ListenBreakActivity : NativeActivity
    {
        public ListenBreakActivity()
        {
            DisplayName = "Break";
        }

        protected override void Execute(NativeActivityContext context)
        {
            if (context.Properties.Find(ListenStepsActivity.ControlPropertyName) == null)
            {
                throw new InvalidOperationException("Listen-Break can only be used inside Listen-Steps.");
            }

            throw new ListenBreakException();
        }
    }

    [ToolboxPath("Listen")]

    [DisplayName("Continue")]
    [Description("Skip the rest of this Listen-Steps iteration and wait for the next packet batch. Does not stop listening.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class ListenContinueActivity : NativeActivity
    {
        public ListenContinueActivity()
        {
            DisplayName = "Continue";
        }

        protected override void Execute(NativeActivityContext context)
        {
            if (context.Properties.Find(ListenStepsActivity.ControlPropertyName) == null)
            {
                throw new InvalidOperationException("Listen-Continue can only be used inside Listen-Steps.");
            }

            throw new ListenContinueException();
        }
    }
}
