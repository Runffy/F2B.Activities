using System;
using System.Activities;
using System.Activities.Statements;
using System.Activities.Validation;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(ReturnIfDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Return If")]
    [Description("When Condition is true, exit the enclosing Traceable TryCatch (same as Return). When false, skip silently. Must be inside Traceable TryCatch.")]
    public sealed class ReturnIfActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public ReturnIfActivity()
        {
            DisplayName = "Return If";
            ExecuteFinally = true;
            Constraints.Add(MustBeInsideTraceableTryCatch());
        }

        [RequiredArgument]
        [DisplayName("Condition")]
        [Description("When true, performs Return. When false, does nothing.")]
        [Category("Input.A")]
        public InArgument<bool> Condition { get; set; }

        [DisplayName("Execute Finally")]
        [Description("True = run Finally before leaving the Traceable TryCatch (default); False = skip Finally. Catch is always skipped.")]
        [Category("Input.B")]
        [DefaultValue(true)]
        public bool ExecuteFinally { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new ReturnIfActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            if (Condition == null || Condition.Expression == null)
            {
                throw new InvalidOperationException("Return If: Condition is required.");
            }

            if (!Condition.Get(context))
            {
                return;
            }

            throw new TraceableReturnSignal(ExecuteFinally);
        }

        private static Constraint MustBeInsideTraceableTryCatch()
        {
            var activityBeingValidated = new DelegateInArgument<ReturnIfActivity>();
            var validationContext = new DelegateInArgument<ValidationContext>();
            var parent = new DelegateInArgument<Activity>();
            var found = new Variable<bool>();

            return new Constraint<ReturnIfActivity>
            {
                Body = new ActivityAction<ReturnIfActivity, ValidationContext>
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
                                    "Return If must be placed inside a Traceable TryCatch.")
                            }
                        }
                    }
                }
            };
        }
    }
}
