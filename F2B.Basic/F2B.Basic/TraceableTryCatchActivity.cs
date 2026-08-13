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
        /// Exception.Source uses a multi-line DisplayName trace across nested Invoke OpenRPA.
        /// FaultXPath keeps the type-based path.
    /// </summary>
    [Designer(typeof(TraceableTryCatchDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Traceable TryCatch")]
    [Description("Try/Catch/Finally that attributes faults to a relative activity path (XPath-like) and DisplayName breadcrumb. Return skips Catch; set Return.Execute Finally=true to still run Finally. Use Traceable Rethrow in Catch to propagate the original exception (keeps Source) after Finally.")]
    public sealed class TraceableTryCatchActivity : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        /// <summary>
        /// Legacy / signal message for early return from TraceableTryCatch (skips Catch; Finally depends on TraceableReturnSignal.ExecuteFinally).
        /// </summary>
        public const string ReturnMessage = "Return";

        private readonly Variable<Exception> _caughtException = new Variable<Exception>("CaughtException");
        private readonly Variable<bool> _exceptionHandled = new Variable<bool>("ExceptionHandled");
        private readonly Variable<bool> _suppressCancel = new Variable<bool>("SuppressCancel");
        private readonly Variable<bool> _skipFinally = new Variable<bool>("SkipFinally");

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
        [Description("Multi-line DisplayName trace from the host workflow root Sequence across nested Invoke OpenRPA.")]
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
            metadata.AddImplementationVariable(_skipFinally);

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
            context.SetValue(_skipFinally, false);

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
            // Early-return signal: swallow, skip Catch; Finally optional (Return.ExecuteFinally).
            if (IsReturnSignal(propagatedException))
            {
                bool executeFinally = GetReturnExecuteFinally(propagatedException);
                AcceptReturnSignal(faultContext, propagatedFrom, skipFinally: !executeFinally);
                if (executeFinally)
                {
                    // OnTryComplete skips Faulted Try and will not schedule Finally — do it here.
                    ScheduleFinally(faultContext);
                }

                return;
            }

            string preferredId = null;
            FaultPathTrackingExtension tracker = faultContext.GetExtension<FaultPathTrackingExtension>();
            if (tracker != null)
            {
                string tryRootId = Try != null ? Try.Id : null;
                preferredId = tracker.ResolveFaultActivityId(tryRootId);
            }

            ActivityFaultPathBuilder.Result fault = ActivityFaultPathBuilder.Build(Try, propagatedFrom, preferredId);
            ActivityFaultPathBuilder.EnrichException(
                propagatedException,
                fault,
                tryCatchActivity: this,
                workflowInstanceId: faultContext.WorkflowInstanceId.ToString());

            faultContext.SetValue(_caughtException, propagatedException);
            WriteFaultOutputs(faultContext, propagatedException, fault);

            // HandleFault alone is not enough: without CancelChild, a Sequence Try can
            // continue scheduling activities after the faulted one (e.g. Log after Throw).
            faultContext.HandleFault();
            if (propagatedFrom != null)
            {
                faultContext.CancelChild(propagatedFrom);
            }

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

        private static bool IsReturnSignal(Exception exception)
        {
            if (exception == null)
            {
                return false;
            }

            if (exception is TraceableReturnSignal)
            {
                return true;
            }

            // Legacy: throw new Exception("Return")
            return string.Equals(exception.Message, ReturnMessage, StringComparison.Ordinal);
        }

        private static bool GetReturnExecuteFinally(Exception exception)
        {
            var signal = exception as TraceableReturnSignal;
            return signal != null && signal.ExecuteFinally;
        }

        private void AcceptReturnSignal(
            NativeActivityFaultContext faultContext,
            ActivityInstance propagatedFrom,
            bool skipFinally)
        {
            faultContext.HandleFault();
            if (propagatedFrom != null)
            {
                faultContext.CancelChild(propagatedFrom);
            }

            faultContext.SetValue(_suppressCancel, true);
            faultContext.SetValue(_exceptionHandled, true);
            faultContext.SetValue(_caughtException, null);
            faultContext.SetValue(_skipFinally, skipFinally);
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
            if (IsReturnSignal(propagatedException))
            {
                bool executeFinally = GetReturnExecuteFinally(propagatedException);
                AcceptReturnSignal(faultContext, propagatedFrom, skipFinally: !executeFinally);
                // Catch completion callback still runs ScheduleFinally and respects _skipFinally.
                return;
            }

            // Rethrow activity: swallow marker, keep original exception, run Finally, then rethrow original (Source intact).
            if (propagatedException is TraceableRethrowSignal)
            {
                faultContext.HandleFault();
                if (propagatedFrom != null)
                {
                    faultContext.CancelChild(propagatedFrom);
                }

                faultContext.SetValue(_suppressCancel, true);
                // Catch was marked handled when scheduled; clear so RethrowIfNeeded throws _caughtException.
                faultContext.SetValue(_exceptionHandled, false);
                return;
            }

            faultContext.SetValue(_suppressCancel, false);
        }

        private void ScheduleFinally(NativeActivityContext context)
        {
            if (context.GetValue(_skipFinally))
            {
                OnFinallyComplete(context, null);
                return;
            }

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
