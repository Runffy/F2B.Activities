using System;
using System.Activities;
using System.Activities.Statements;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(AsyncFormDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Async Form")]
    [Description("Open a JSON WinForms UI, run Init once, loop BindEvent handlers, optional Close Scope on user close.")]
    public sealed class AsyncFormActivity : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        private readonly Variable<FormSession> _session = new Variable<FormSession>("FormSession");
        private readonly Variable<int> _registerIndex = new Variable<int>("RegisterIndex");
        private readonly Collection<BindEventActivity> _bindEvents = new Collection<BindEventActivity>();
        private BookmarkCallback _eventBookmarkCallback;

        public AsyncFormActivity()
        {
            DisplayName = "Async Form";
            Timeout = new InArgument<int>(0);
            _eventBookmarkCallback = OnFormEvent;
        }

        [RequiredArgument]
        [DisplayName("Form Path")]
        [Description("Absolute path to the form JSON file.")]
        [Category("Input.A")]
        public InArgument<string> FormPath { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Auto-close after this many milliseconds. 0 = no timeout.")]
        [Category("Input.B")]
        public InArgument<int> Timeout { get; set; }

        [DisplayName("Result Json")]
        [Category("Output")]
        public OutArgument<string> ResultJson { get; set; }

        [DisplayName("Error Message")]
        [Category("Output")]
        public OutArgument<string> ErrorMessage { get; set; }

        [Browsable(false)]
        public Activity Init { get; set; }

        /// <summary>
        /// Runs when the user tries to close the form (X). Cancelled until this scope calls Close Form (or form otherwise closes).
        /// Null = allow immediate user close.
        /// </summary>
        [Browsable(false)]
        public Activity Close { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public Collection<BindEventActivity> BindEvents
        {
            get { return _bindEvents; }
        }

        public Activity Create(DependencyObject target)
        {
            return new AsyncFormActivity
            {
                DisplayName = "Async Form",
                Timeout = new InArgument<int>(0),
                Init = new Sequence { DisplayName = "Init Scope" }
                // Close left null — drop activities into Close Scope only when intercepting user close.
            };
        }

        protected override bool CanInduceIdle
        {
            get { return true; }
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            var formPathArg = new RuntimeArgument("FormPath", typeof(string), ArgumentDirection.In);
            metadata.Bind(FormPath, formPathArg);
            metadata.AddArgument(formPathArg);

            var timeoutArg = new RuntimeArgument("Timeout", typeof(int), ArgumentDirection.In);
            metadata.Bind(Timeout, timeoutArg);
            metadata.AddArgument(timeoutArg);

            var resultJsonArg = new RuntimeArgument("ResultJson", typeof(string), ArgumentDirection.Out);
            metadata.Bind(ResultJson, resultJsonArg);
            metadata.AddArgument(resultJsonArg);

            var errorMessageArg = new RuntimeArgument("ErrorMessage", typeof(string), ArgumentDirection.Out);
            metadata.Bind(ErrorMessage, errorMessageArg);
            metadata.AddArgument(errorMessageArg);

            if (Init != null)
            {
                metadata.AddChild(Init);
            }

            if (Close != null)
            {
                metadata.AddChild(Close);
            }

            foreach (BindEventActivity bindEvent in _bindEvents)
            {
                if (bindEvent != null)
                {
                    metadata.AddChild(bindEvent);
                }
            }

            metadata.AddImplementationVariable(_session);
            metadata.AddImplementationVariable(_registerIndex);
            metadata.AddDefaultExtensionProvider(() => new WorkflowInstanceExtension());
            metadata.AddDefaultExtensionProvider(() => new FormSessionHolder());
        }

        protected override void Execute(NativeActivityContext context)
        {
            string formPath = FormPath.Get(context);
            int timeoutMs = Timeout.Get(context);
            if (timeoutMs < 0)
            {
                timeoutMs = 0;
            }

            WorkflowInstanceExtension workflow = context.GetExtension<WorkflowInstanceExtension>();
            if (workflow == null)
            {
                throw new InvalidOperationException("WorkflowInstanceExtension is required.");
            }

            var session = new FormSession();
            try
            {
                session.Open(formPath, timeoutMs, workflow);
            }
            catch (Exception ex)
            {
                ResultJson.Set(context, null);
                ErrorMessage.Set(context, ex.Message);
                throw;
            }

            context.SetValue(_session, session);
            FormSessionHolder holder = context.GetExtension<FormSessionHolder>();
            if (holder != null)
            {
                holder.Current = session;
            }

            session.InterceptUserClose = Close != null;
            session.IsRegistering = true;
            context.SetValue(_registerIndex, 0);

            RegisterNext(context);
        }

        protected override void Cancel(NativeActivityContext context)
        {
            FormSession session = context.GetValue(_session);
            if (session != null)
            {
                session.AbortFromWorkflow("Workflow cancelled.");
                CompleteWithOutputs(context, session);
            }

            ClearHolder(context);
            base.Cancel(context);
        }

        protected override void Abort(NativeActivityAbortContext context)
        {
            FormSessionHolder holder = context.GetExtension<FormSessionHolder>();
            FormSession session = holder == null ? null : holder.Current;
            if (session != null)
            {
                try
                {
                    session.AbortFromWorkflow("Workflow aborted.");
                }
                catch
                {
                    // ignored
                }

                if (holder != null)
                {
                    holder.Current = null;
                }
            }

            base.Abort(context);
        }

        private void RegisterNext(NativeActivityContext context)
        {
            int index = context.GetValue(_registerIndex);
            if (index < _bindEvents.Count)
            {
                BindEventActivity bind = _bindEvents[index];
                context.SetValue(_registerIndex, index + 1);
                if (bind == null)
                {
                    RegisterNext(context);
                    return;
                }

                context.ScheduleActivity(bind, OnRegisterComplete, OnChildFault);
                return;
            }

            // Registration done — run Init.
            FormSession session = context.GetValue(_session);
            if (session != null)
            {
                session.IsRegistering = false;
            }

            if (Init != null)
            {
                context.ScheduleActivity(Init, OnInitComplete, OnChildFault);
            }
            else
            {
                EnterEventLoop(context);
            }
        }

        private void OnRegisterComplete(NativeActivityContext context, ActivityInstance completed)
        {
            RegisterNext(context);
        }

        private void OnInitComplete(NativeActivityContext context, ActivityInstance completed)
        {
            EnterEventLoop(context);
        }

        private void EnterEventLoop(NativeActivityContext context)
        {
            FormSession session = context.GetValue(_session);
            if (session == null || session.IsClosed)
            {
                CompleteWithOutputs(context, session);
                return;
            }

            // Process any already-queued events, else idle on bookmark.
            DispatchNextEventOrIdle(context, session);
        }

        private void OnFormEvent(NativeActivityContext context, Bookmark bookmark, object value)
        {
            FormSession session = context.GetValue(_session);
            if (session == null)
            {
                return;
            }

            DispatchNextEventOrIdle(context, session);
        }

        private void DispatchNextEventOrIdle(NativeActivityContext context, FormSession session)
        {
            if (session.IsClosed)
            {
                // Allow one last Closed/Closing dispatch if queued.
            }

            if (!session.TryDequeueEvent(out FormEvent formEvent))
            {
                if (session.IsClosed)
                {
                    CompleteWithOutputs(context, session);
                    return;
                }

                context.CreateBookmark(FormSession.EventBookmarkName, _eventBookmarkCallback);
                return;
            }

            session.LastControlId = formEvent.ControlId;
            session.LastEventName = formEvent.EventName;

            if (IsFormClosingEvent(formEvent))
            {
                if (Close != null)
                {
                    session.BeginHandler(UiBehavior.LockQueue);
                    context.ScheduleActivity(Close, OnHandlerComplete, OnHandlerFault);
                }
                else
                {
                    DispatchNextEventOrIdle(context, session);
                }

                return;
            }

            if (!session.TryGetBindings(formEvent.ControlId, formEvent.EventName, out var bindings)
                || bindings == null
                || bindings.Count == 0)
            {
                // No handler — if form closed, complete; else continue loop.
                if (session.IsClosed
                    && string.Equals(formEvent.EventName, "Closed", StringComparison.OrdinalIgnoreCase))
                {
                    CompleteWithOutputs(context, session);
                    return;
                }

                DispatchNextEventOrIdle(context, session);
                return;
            }

            // Run first matching binding (V1: one handler per event key recommended).
            FormSession.BoundEvent bound = bindings[0];
            Activity bindHost = bound.ActivityKey as Activity;
            if (!(bindHost is BindEventActivity) && !(bindHost is DynamicBindEventActivity))
            {
                bindHost = null;
            }

            if (bindHost == null)
            {
                DispatchNextEventOrIdle(context, session);
                return;
            }

            session.BeginHandler(bound.Behavior);
            context.ScheduleActivity(bindHost, OnHandlerComplete, OnHandlerFault);
        }

        private static bool IsFormClosingEvent(FormEvent formEvent)
        {
            return formEvent != null
                && string.Equals(formEvent.ControlId, "form", StringComparison.OrdinalIgnoreCase)
                && string.Equals(formEvent.EventName, "Closing", StringComparison.OrdinalIgnoreCase);
        }

        private void OnHandlerComplete(NativeActivityContext context, ActivityInstance completed)
        {
            FormSession session = context.GetValue(_session);
            if (session != null)
            {
                session.EndHandler();
            }

            if (session == null || session.IsClosed)
            {
                CompleteWithOutputs(context, session);
                return;
            }

            DispatchNextEventOrIdle(context, session);
        }

        private void OnHandlerFault(
            NativeActivityFaultContext faultContext,
            Exception propagatedException,
            ActivityInstance propagatedFrom)
        {
            FormSession session = faultContext.GetValue(_session);
            if (session != null)
            {
                session.EndHandler();
                session.Close(FormCloseReason.Error, propagatedException == null ? "Handler faulted." : propagatedException.Message);
                try
                {
                    ResultJson.Set(faultContext, session.BuildResultJson());
                    ErrorMessage.Set(faultContext, session.ErrorMessage);
                }
                catch
                {
                    // ignored
                }

                session.WaitUiClosed(3000);
            }

            // Let the fault propagate (unhandled BindEvent exception).
        }

        private void OnChildFault(
            NativeActivityFaultContext faultContext,
            Exception propagatedException,
            ActivityInstance propagatedFrom)
        {
            FormSession session = faultContext.GetValue(_session);
            if (session != null)
            {
                session.Close(FormCloseReason.Error, propagatedException == null ? "Fault." : propagatedException.Message);
                try
                {
                    ResultJson.Set(faultContext, session.BuildResultJson());
                    ErrorMessage.Set(faultContext, session.ErrorMessage);
                }
                catch
                {
                    // ignored
                }

                session.WaitUiClosed(3000);
            }

            // Let the fault propagate.
        }

        private void CompleteWithOutputs(NativeActivityContext context, FormSession session)
        {
            if (session != null)
            {
                try
                {
                    ResultJson.Set(context, session.BuildResultJson());
                    ErrorMessage.Set(context, session.ErrorMessage);
                }
                catch (Exception ex)
                {
                    ErrorMessage.Set(context, ex.Message);
                }

                session.WaitUiClosed(3000);
                session.Dispose();
                context.SetValue(_session, null);
                ClearHolder(context);
            }
        }

        private static void ClearHolder(NativeActivityContext context)
        {
            FormSessionHolder holder = context.GetExtension<FormSessionHolder>();
            if (holder != null)
            {
                holder.Current = null;
            }
        }
    }
}
