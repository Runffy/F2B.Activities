using System;
using System.Activities;
using System.Activities.Statements;
using System.Activities.Validation;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    /// <summary>
    /// Early-exit for <see cref="TraceableTryCatchActivity"/>: throws Message = "Return".
    /// Must be placed inside a Traceable TryCatch (validated on the workflow canvas).
    /// </summary>
    [Designer(typeof(BasicSimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Return")]
    [Description("Exit the enclosing Traceable TryCatch without running Catch/Finally. Must be inside Traceable TryCatch.")]
    public sealed class ReturnActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public ReturnActivity()
        {
            DisplayName = "Return";
            Constraints.Add(MustBeInsideTraceableTryCatch());
        }

        public Activity Create(DependencyObject target)
        {
            return new ReturnActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            throw new Exception(TraceableTryCatchActivity.ReturnMessage);
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
