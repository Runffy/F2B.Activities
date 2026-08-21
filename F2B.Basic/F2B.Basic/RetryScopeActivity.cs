using System;
using System.Activities;
using System.Activities.Statements;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    public enum RetryScopeMode
    {
        ByTimes,
        ByTimeout
    }

    /// <summary>
    /// Runs Retry Body then Assert Body. Fault in either body triggers another attempt
    /// after Retry Interval, until success or the Retry Time limit (by count or by timeout) is reached.
    /// </summary>
    [Designer(typeof(RetryScopeDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Retry Scope")]
    [Description("Execute Retry Body then Assert Body. Choose By Times or By Timeout; Retry Time is max attempts or duration (ms).")]
    public sealed class RetryScopeActivity : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public const string DefaultRetryCounterName = "retry_counter";

        private readonly Variable<int> _attempt = new Variable<int>("Attempt");
        private readonly Variable<RetryScopeMode> _mode = new Variable<RetryScopeMode>("Mode");
        private readonly Variable<int> _retryTime = new Variable<int>("RetryTime");
        private readonly Variable<int> _intervalMs = new Variable<int>("IntervalMs");
        private readonly Variable<DateTime> _deadlineUtc = new Variable<DateTime>("DeadlineUtc");
        private readonly Variable<string> _failMessage = new Variable<string>("FailMessage");
        private readonly Variable<Exception> _lastException = new Variable<Exception>("LastException");
        private readonly Variable<bool> _suppressCancel = new Variable<bool>("SuppressCancel");
        private readonly Variable<TimeSpan> _delayDuration = new Variable<TimeSpan>("DelayDuration");

        private Delay _delay;

        public RetryScopeActivity()
        {
            DisplayName = "Retry Scope";
            RetryMode = RetryScopeMode.ByTimes;
            RetryTime = new InArgument<int>(3);
            RetryInterval = new InArgument<int>(1000);
            EnsureRetryBodyAction(null);
            EnsureAssertBodyAction(null);
        }

        [DisplayName("Retry Mode")]
        [Description("By Times: Retry Time is max attempts (including the first). By Timeout: Retry Time is max duration in milliseconds.")]
        [Category("Input.A")]
        [DefaultValue(RetryScopeMode.ByTimes)]
        public RetryScopeMode RetryMode { get; set; }

        [RequiredArgument]
        [DisplayName("Retry Time")]
        [Description("By Times: maximum attempts including the first (> 0). By Timeout: maximum wall-clock duration in ms (> 0).")]
        [Category("Input.A")]
        public InArgument<int> RetryTime { get; set; }

        [DisplayName("Retry Interval (ms)")]
        [Description("Delay in milliseconds between two attempts. No delay before the first attempt.")]
        [Category("Input.A")]
        public InArgument<int> RetryInterval { get; set; }

        [DisplayName("Exception Message")]
        [Description("If all attempts fail: when not null/whitespace, throw an Exception with this message (last fault as InnerException). Otherwise rethrow the last fault.")]
        [Category("Input.A")]
        public InArgument<string> ExceptionMessage { get; set; }

        /// <summary>
        /// Name of the 1-based attempt counter available inside Retry Body / Assert Body (like Catch's exception).
        /// </summary>
        [DisplayName("Retry Counter")]
        [Description("Variable name for the current attempt (1-based) inside Retry Body and Assert Body. Default: retry_counter.")]
        [Category("Input.A")]
        [DefaultValue(DefaultRetryCounterName)]
        public string RetryCounter
        {
            get
            {
                EnsureRetryBodyAction(null);
                EnsureAssertBodyAction(null);
                return RetryBody.Argument.Name;
            }
            set
            {
                string name = NormalizeCounterName(value);
                EnsureRetryBodyAction(null);
                EnsureAssertBodyAction(null);
                RetryBody.Argument.Name = name;
                AssertBody.Argument.Name = name;
            }
        }

        [Browsable(false)]
        public ActivityAction<int> RetryBody { get; set; }

        [Browsable(false)]
        public ActivityAction<int> AssertBody { get; set; }

        protected override bool CanInduceIdle
        {
            get { return true; }
        }

        public Activity Create(DependencyObject target)
        {
            return new RetryScopeActivity
            {
                DisplayName = "Retry Scope",
                RetryMode = RetryScopeMode.ByTimes,
                RetryTime = new InArgument<int>(3),
                RetryInterval = new InArgument<int>(1000),
                RetryBody = CreateCounterAction(new Sequence { DisplayName = "Retry Body" }, DefaultRetryCounterName),
                AssertBody = CreateCounterAction(new Sequence { DisplayName = "Assert Body" }, DefaultRetryCounterName)
            };
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            EnsureRetryBodyAction(null);
            EnsureAssertBodyAction(null);
            SyncCounterArgumentNames();

            BindInArgument(metadata, RetryTime, "RetryTime", typeof(int));
            BindInArgument(metadata, RetryInterval, "RetryInterval", typeof(int));
            BindInArgument(metadata, ExceptionMessage, "ExceptionMessage", typeof(string));

            if (RetryTime == null || RetryTime.Expression == null)
            {
                metadata.AddValidationError("Retry Scope: Retry Time is required.");
            }

            if (RetryBody != null)
            {
                metadata.AddDelegate(RetryBody);
            }

            if (AssertBody != null)
            {
                metadata.AddDelegate(AssertBody);
            }

            _delay = new Delay
            {
                Duration = new InArgument<TimeSpan>(_delayDuration)
            };
            metadata.AddImplementationChild(_delay);

            metadata.AddImplementationVariable(_attempt);
            metadata.AddImplementationVariable(_mode);
            metadata.AddImplementationVariable(_retryTime);
            metadata.AddImplementationVariable(_intervalMs);
            metadata.AddImplementationVariable(_deadlineUtc);
            metadata.AddImplementationVariable(_failMessage);
            metadata.AddImplementationVariable(_lastException);
            metadata.AddImplementationVariable(_suppressCancel);
            metadata.AddImplementationVariable(_delayDuration);
        }

        protected override void Execute(NativeActivityContext context)
        {
            int retryTime = RetryTime != null ? RetryTime.Get(context) : 0;
            if (retryTime <= 0)
            {
                throw new InvalidOperationException("Retry Scope: Retry Time must be an integer > 0.");
            }

            int intervalMs = RetryInterval != null ? RetryInterval.Get(context) : 0;
            if (intervalMs < 0)
            {
                intervalMs = 0;
            }

            string failMessage = ExceptionMessage != null ? ExceptionMessage.Get(context) : null;
            RetryScopeMode mode = RetryMode;

            context.SetValue(_attempt, 0);
            context.SetValue(_mode, mode);
            context.SetValue(_retryTime, retryTime);
            context.SetValue(_intervalMs, intervalMs);
            context.SetValue(
                _deadlineUtc,
                mode == RetryScopeMode.ByTimeout
                    ? DateTime.UtcNow.AddMilliseconds(retryTime)
                    : DateTime.MaxValue);
            context.SetValue(_failMessage, failMessage);
            context.SetValue(_lastException, null);
            context.SetValue(_suppressCancel, false);

            ScheduleAttempt(context);
        }

        protected override void Cancel(NativeActivityContext context)
        {
            if (!context.GetValue(_suppressCancel))
            {
                context.CancelChildren();
            }
        }

        private void ScheduleAttempt(NativeActivityContext context)
        {
            int attempt = context.GetValue(_attempt) + 1;
            context.SetValue(_attempt, attempt);

            if (RetryBody != null && RetryBody.Handler != null)
            {
                context.ScheduleAction(RetryBody, attempt, OnRetryBodyComplete, OnRetryBodyFault);
                return;
            }

            ScheduleAssert(context, attempt);
        }

        private void OnRetryBodyFault(
            NativeActivityFaultContext faultContext,
            Exception propagatedException,
            ActivityInstance propagatedFrom)
        {
            AcceptFault(faultContext, propagatedFrom);
            OnAttemptFailed(faultContext, propagatedException);
        }

        private void OnRetryBodyComplete(NativeActivityContext context, ActivityInstance completedInstance)
        {
            if (completedInstance != null && completedInstance.State == ActivityInstanceState.Faulted)
            {
                return;
            }

            ScheduleAssert(context, context.GetValue(_attempt));
        }

        private void ScheduleAssert(NativeActivityContext context, int attempt)
        {
            if (AssertBody != null && AssertBody.Handler != null)
            {
                context.ScheduleAction(AssertBody, attempt, OnAssertBodyComplete, OnAssertBodyFault);
                return;
            }

            // No assert body: Retry Body success ends the scope.
        }

        private void OnAssertBodyFault(
            NativeActivityFaultContext faultContext,
            Exception propagatedException,
            ActivityInstance propagatedFrom)
        {
            AcceptFault(faultContext, propagatedFrom);
            OnAttemptFailed(faultContext, propagatedException);
        }

        private void OnAssertBodyComplete(NativeActivityContext context, ActivityInstance completedInstance)
        {
            // Success: leave the activity.
        }

        private void OnAttemptFailed(NativeActivityContext context, Exception exception)
        {
            context.SetValue(_lastException, exception);

            if (!CanRetryAgain(context))
            {
                ThrowFinal(context);
                return;
            }

            int intervalMs = context.GetValue(_intervalMs);
            if (intervalMs <= 0)
            {
                context.SetValue(_suppressCancel, false);
                if (!CanRetryAgain(context))
                {
                    ThrowFinal(context);
                    return;
                }

                ScheduleAttempt(context);
                return;
            }

            context.SetValue(_delayDuration, TimeSpan.FromMilliseconds(intervalMs));
            context.ScheduleActivity(_delay, OnDelayComplete);
        }

        private void OnDelayComplete(NativeActivityContext context, ActivityInstance completedInstance)
        {
            context.SetValue(_suppressCancel, false);
            if (!CanRetryAgain(context))
            {
                ThrowFinal(context);
                return;
            }

            ScheduleAttempt(context);
        }

        private bool CanRetryAgain(NativeActivityContext context)
        {
            RetryScopeMode mode = context.GetValue(_mode);
            if (mode == RetryScopeMode.ByTimeout)
            {
                DateTime deadline = context.GetValue(_deadlineUtc);
                return DateTime.UtcNow < deadline;
            }

            int attempt = context.GetValue(_attempt);
            int maxAttempts = context.GetValue(_retryTime);
            return attempt < maxAttempts;
        }

        private void ThrowFinal(NativeActivityContext context)
        {
            Exception last = context.GetValue(_lastException);
            string message = context.GetValue(_failMessage);

            if (!string.IsNullOrWhiteSpace(message))
            {
                throw new Exception(message.Trim(), last);
            }

            if (last != null)
            {
                throw last;
            }

            RetryScopeMode mode = context.GetValue(_mode);
            throw new Exception(
                mode == RetryScopeMode.ByTimeout
                    ? "Retry Scope: retry timeout elapsed."
                    : "Retry Scope: all attempts failed.");
        }

        private void AcceptFault(NativeActivityFaultContext faultContext, ActivityInstance propagatedFrom)
        {
            faultContext.HandleFault();
            if (propagatedFrom != null)
            {
                faultContext.CancelChild(propagatedFrom);
            }

            faultContext.SetValue(_suppressCancel, true);
        }

        private void EnsureRetryBodyAction(Activity handler)
        {
            if (RetryBody == null)
            {
                RetryBody = CreateCounterAction(handler, DefaultRetryCounterName);
                return;
            }

            EnsureArgument(RetryBody, DefaultRetryCounterName);
        }

        private void EnsureAssertBodyAction(Activity handler)
        {
            if (AssertBody == null)
            {
                AssertBody = CreateCounterAction(handler, DefaultRetryCounterName);
                return;
            }

            EnsureArgument(AssertBody, DefaultRetryCounterName);
        }

        private void SyncCounterArgumentNames()
        {
            string name = NormalizeCounterName(
                RetryBody != null && RetryBody.Argument != null
                    ? RetryBody.Argument.Name
                    : DefaultRetryCounterName);

            if (RetryBody != null && RetryBody.Argument != null)
            {
                RetryBody.Argument.Name = name;
            }

            if (AssertBody != null && AssertBody.Argument != null)
            {
                AssertBody.Argument.Name = name;
            }
        }

        private static void EnsureArgument(ActivityAction<int> action, string defaultName)
        {
            if (action.Argument == null)
            {
                action.Argument = new DelegateInArgument<int>(defaultName);
            }
            else if (string.IsNullOrWhiteSpace(action.Argument.Name))
            {
                action.Argument.Name = defaultName;
            }
        }

        private static ActivityAction<int> CreateCounterAction(Activity handler, string counterName)
        {
            return new ActivityAction<int>
            {
                Argument = new DelegateInArgument<int>(NormalizeCounterName(counterName)),
                Handler = handler
            };
        }

        private static string NormalizeCounterName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? DefaultRetryCounterName : value.Trim();
        }

        private static void BindInArgument(
            NativeActivityMetadata metadata,
            Argument argument,
            string name,
            Type type)
        {
            if (argument == null)
            {
                return;
            }

            var runtimeArgument = new RuntimeArgument(name, type, ArgumentDirection.In);
            metadata.Bind(argument, runtimeArgument);
            metadata.AddArgument(runtimeArgument);
        }
    }
}
