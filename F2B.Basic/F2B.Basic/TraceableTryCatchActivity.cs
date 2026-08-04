using System;
using System.Activities;
using System.Activities.Statements;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    /// <summary>
    /// Try / Catch / Finally with fault attribution: Activity Id, DisplayName, relative XPath, and DisplayName path.
    /// Catch is an ActivityAction&lt;Exception&gt; so the handler argument <c>exception</c> is in scope (like WF TryCatch).
    /// Exception.Source uses a DisplayName path (same-name sibling index). FaultXPath keeps the type-based path.
    /// </summary>
    [Designer(typeof(TraceableTryCatchDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Traceable TryCatch")]
    [Description("Try/Catch/Finally that attributes faults to a relative activity path (XPath-like) and DisplayName breadcrumb.")]
    public sealed class TraceableTryCatchActivity : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        private readonly Variable<Exception> _caughtException = new Variable<Exception>("CaughtException");
        private readonly Variable<bool> _exceptionHandled = new Variable<bool>("ExceptionHandled");
        private readonly Variable<bool> _suppressCancel = new Variable<bool>("SuppressCancel");

        public TraceableTryCatchActivity()
        {
            DisplayName = "Traceable TryCatch";
            EnsureCatchAction();
        }

        [Browsable(false)]
        public Activity Try { get; set; }

        /// <summary>
        /// Catch handler. Use the delegate argument <c>exception</c> in expressions (e.g. exception.Source).
        /// </summary>
        [Browsable(false)]
        public ActivityAction<Exception> Catch { get; set; }

        [Browsable(false)]
        public Activity Finally { get; set; }

        [DisplayName("Exception")]
        [Description("Optional: also copy the caught exception to a workflow variable.")]
        [Category("Output")]
        public OutArgument<Exception> Exception { get; set; }

        [DisplayName("Fault Activity Id")]
        [Category("Output")]
        public OutArgument<string> FaultActivityId { get; set; }

        [DisplayName("Fault Display Name")]
        [Category("Output")]
        public OutArgument<string> FaultDisplayName { get; set; }

        [DisplayName("Fault XPath")]
        [Description("Try-relative structural path, e.g. //sequence/sequence[1]/assign[5] (0-based same-type index).")]
        [Category("Output")]
        public OutArgument<string> FaultXPath { get; set; }

        [DisplayName("Fault Display Path")]
        [Description("Try-relative DisplayName path with 0-based index among same DisplayName siblings, e.g. Try/Sequence[1]/some loop/error point.")]
        [Category("Output")]
        public OutArgument<string> FaultDisplayPath { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new TraceableTryCatchActivity
            {
                DisplayName = "Traceable TryCatch",
                Try = new Sequence { DisplayName = "Try" },
                Catch = CreateCatchAction(new Sequence { DisplayName = "Catch" }),
                Finally = new Sequence { DisplayName = "Finally" }
            };
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            EnsureCatchAction();

            if (Try != null)
            {
                metadata.AddChild(Try);
            }

            if (Catch != null)
            {
                metadata.AddDelegate(Catch);
            }

            if (Finally != null)
            {
                metadata.AddChild(Finally);
            }

            metadata.AddImplementationVariable(_caughtException);
            metadata.AddImplementationVariable(_exceptionHandled);
            metadata.AddImplementationVariable(_suppressCancel);

            // Tracking extension: first Faulted Id (leaf) for accurate path attribution.
            metadata.AddDefaultExtensionProvider(() => new FaultPathTrackingExtension());

            AddOutArgument(metadata, Exception, "Exception", typeof(Exception));
            AddOutArgument(metadata, FaultActivityId, "FaultActivityId", typeof(string));
            AddOutArgument(metadata, FaultDisplayName, "FaultDisplayName", typeof(string));
            AddOutArgument(metadata, FaultXPath, "FaultXPath", typeof(string));
            AddOutArgument(metadata, FaultDisplayPath, "FaultDisplayPath", typeof(string));
        }

        protected override void Execute(NativeActivityContext context)
        {
            context.SetValue(_caughtException, null);
            context.SetValue(_exceptionHandled, false);
            context.SetValue(_suppressCancel, false);

            FaultPathTrackingExtension tracker = context.GetExtension<FaultPathTrackingExtension>();
            if (tracker != null)
            {
                tracker.Reset();
            }

            if (Try != null)
            {
                context.ScheduleActivity(Try, OnTryComplete, OnExceptionFromTry);
            }
            else
            {
                ScheduleFinally(context);
            }
        }

        protected override void Cancel(NativeActivityContext context)
        {
            if (!context.GetValue(_suppressCancel))
            {
                context.CancelChildren();
            }
        }

        private void OnExceptionFromTry(NativeActivityFaultContext faultContext, Exception propagatedException, ActivityInstance propagatedFrom)
        {
            string preferredId = null;
            FaultPathTrackingExtension tracker = faultContext.GetExtension<FaultPathTrackingExtension>();
            if (tracker != null)
            {
                string tryRootId = Try != null ? Try.Id : null;
                preferredId = tracker.ResolveFaultActivityId(tryRootId);
            }

            ActivityFaultPathBuilder.Result fault = ActivityFaultPathBuilder.Build(Try, propagatedFrom, preferredId);
            ActivityFaultPathBuilder.EnrichException(propagatedException, fault);

            faultContext.SetValue(_caughtException, propagatedException);
            WriteFaultOutputs(faultContext, propagatedException, fault);

            faultContext.HandleFault();
            faultContext.SetValue(_suppressCancel, true);

            if (Catch != null && Catch.Handler != null)
            {
                faultContext.SetValue(_exceptionHandled, true);
                faultContext.ScheduleAction(Catch, propagatedException, OnCatchComplete, OnExceptionFromCatchOrFinally);
            }
            else
            {
                ScheduleFinally(faultContext);
            }
        }

        private void OnTryComplete(NativeActivityContext context, ActivityInstance completedInstance)
        {
            if (completedInstance != null && completedInstance.State == ActivityInstanceState.Faulted)
            {
                return;
            }

            ScheduleFinally(context);
        }

        private void OnCatchComplete(NativeActivityContext context, ActivityInstance completedInstance)
        {
            context.SetValue(_suppressCancel, false);
            ScheduleFinally(context);
        }

        private void OnExceptionFromCatchOrFinally(NativeActivityFaultContext faultContext, Exception propagatedException, ActivityInstance propagatedFrom)
        {
            faultContext.SetValue(_suppressCancel, false);
        }

        private void ScheduleFinally(NativeActivityContext context)
        {
            if (Finally != null)
            {
                context.ScheduleActivity(Finally, OnFinallyComplete, OnExceptionFromCatchOrFinally);
            }
            else
            {
                OnFinallyComplete(context, null);
            }
        }

        private void OnFinallyComplete(NativeActivityContext context, ActivityInstance completedInstance)
        {
            if (context.IsCancellationRequested && !context.GetValue(_exceptionHandled))
            {
                context.MarkCanceled();
                return;
            }

            RethrowIfNeeded(context);
        }

        private void RethrowIfNeeded(NativeActivityContext context)
        {
            if (context.GetValue(_exceptionHandled))
            {
                return;
            }

            Exception pending = context.GetValue(_caughtException);
            if (pending != null)
            {
                throw pending;
            }
        }

        private void WriteFaultOutputs(ActivityContext context, Exception exception, ActivityFaultPathBuilder.Result fault)
        {
            if (Exception != null)
            {
                context.SetValue(Exception, exception);
            }

            if (fault == null)
            {
                return;
            }

            if (FaultActivityId != null)
            {
                context.SetValue(FaultActivityId, fault.ActivityId ?? string.Empty);
            }

            if (FaultDisplayName != null)
            {
                context.SetValue(FaultDisplayName, fault.DisplayName ?? string.Empty);
            }

            if (FaultXPath != null)
            {
                context.SetValue(FaultXPath, fault.XPath ?? string.Empty);
            }

            if (FaultDisplayPath != null)
            {
                context.SetValue(FaultDisplayPath, fault.DisplayPath ?? string.Empty);
            }
        }

        private void EnsureCatchAction()
        {
            if (Catch == null)
            {
                Catch = CreateCatchAction(null);
                return;
            }

            if (Catch.Argument == null)
            {
                Catch.Argument = new DelegateInArgument<Exception>("exception");
            }
            else if (string.IsNullOrWhiteSpace(Catch.Argument.Name))
            {
                Catch.Argument.Name = "exception";
            }
        }

        private static ActivityAction<Exception> CreateCatchAction(Activity handler)
        {
            return new ActivityAction<Exception>
            {
                Argument = new DelegateInArgument<Exception>("exception"),
                Handler = handler
            };
        }

        private static void AddOutArgument(NativeActivityMetadata metadata, Argument argument, string name, Type type)
        {
            if (argument == null)
            {
                return;
            }

            var runtimeArgument = new RuntimeArgument(name, type, ArgumentDirection.Out);
            metadata.Bind(argument, runtimeArgument);
            metadata.AddArgument(runtimeArgument);
        }
    }
}
