using System;
using System.Activities;
using System.Activities.Statements;
using System.Activities.Validation;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    /// <summary>
    /// Marker used by <see cref="ReturnActivity"/> so Traceable TryCatch can early-exit
    /// without Catch, optionally running Finally.
    /// </summary>
    internal sealed class TraceableReturnSignal : Exception
    {
        public TraceableReturnSignal(bool executeFinally)
            : base(TraceableTryCatchActivity.ReturnMessage)
        {
            ExecuteFinally = executeFinally;
        }

        public bool ExecuteFinally { get; }
    }

    /// <summary>
    /// Early-exit for <see cref="TraceableTryCatchActivity"/>.
    /// Always skips Catch. Finally runs only when <see cref="ExecuteFinally"/> is true.
    /// Must be placed inside a Traceable TryCatch (validated on the workflow canvas).
    /// </summary>
    [Designer(typeof(BasicSimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Return")]
    [Description("Exit the enclosing Traceable TryCatch without Catch. Optionally run Finally (Execute Finally). Must be inside Traceable TryCatch.")]
    public sealed class ReturnActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public ReturnActivity()
        {
            DisplayName = "Return";
            ExecuteFinally = true;
            Constraints.Add(MustBeInsideTraceableTryCatch());
        }

        /// <summary>
        /// Plain bool (not InArgument) so the property pane shows a True/False dropdown.
        /// Default true: run Finally (closer to try/finally); set false to skip cleanup.
        /// </summary>
        [DisplayName("Execute Finally")]
        [Description("True = run Finally before leaving the Traceable TryCatch (default); False = skip Finally. Catch is always skipped.")]
        [Category("Input")]
        [DefaultValue(true)]
        public bool ExecuteFinally { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new ReturnActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            throw new TraceableReturnSignal(ExecuteFinally);
        }

        private static Constraint MustBeInsideTraceableTryCatch()
        {
            var activityBeingValidated = new DelegateInArgument<ReturnActivity>();
            var validationContext = new DelegateInArgument<ValidationContext>();
            var parent = new DelegateInArgument<Activity>();
            var found = new Variable<bool>();

            return new Constraint<ReturnActivity>
            {
                Body = new ActivityAction<ReturnActivity, ValidationContext>
                {
                    Argument1 = activityBeingValidated,
                    Argument2 = validationContext,
                    Handler = new Sequence
                    {
                        Variables = { found },
                        Activities =
                        {
                            new ForEach<Activity>
                            {
                                Values = new GetParentChain
                                {
                                    ValidationContext = validationContext
                                },
                                Body = new ActivityAction<Activity>
                                {
                                    Argument = parent,
                                    Handler = new If
                                    {
                                        Condition = new InArgument<bool>(
                                            env => parent.Get(env) is TraceableTryCatchActivity),
                                        Then = new Assign<bool>
                                        {
                                            To = found,
                                            Value = true
                                        }
                                    }
                                }
                            },
                            new AssertValidation
                            {
                                Assertion = new InArgument<bool>(found),
                                Message = new InArgument<string>(
                                    "Return must be placed inside a Traceable TryCatch.")
                            }
                        }
                    }
                }
            };
        }
    }
}
