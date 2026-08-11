using System;
using System.Activities;
using System.Activities.Statements;
using System.Activities.Validation;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace F2B.Basic
{
    /// <summary>
    /// Marker used by <see cref="TraceableRethrowActivity"/> so Traceable TryCatch can
    /// rethrow the original caught exception after Finally (preserving Source / fault path).
    /// </summary>
    internal sealed class TraceableRethrowSignal : Exception
    {
        public TraceableRethrowSignal()
            : base("Rethrow")
        {
        }
    }

    /// <summary>
    /// Traceable-TryCatch equivalent of WF Rethrow: rethrows the original caught exception
    /// after Finally, without replacing fault Source with this activity's location.
    /// Must be placed inside Traceable TryCatch → Catch.
    /// </summary>
    [Designer(typeof(BasicSimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Traceable Rethrow")]
    [Description("Rethrow the original exception from Traceable TryCatch Catch (preserves Source). Must be inside Catch.")]
    public sealed class TraceableRethrowActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public TraceableRethrowActivity()
        {
            DisplayName = "Traceable Rethrow";
            Constraints.Add(MustBeInsideTraceableTryCatchCatch());
        }

        public Activity Create(DependencyObject target)
        {
            return new TraceableRethrowActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            throw new TraceableRethrowSignal();
        }

        private static Constraint MustBeInsideTraceableTryCatchCatch()
        {
            var activityBeingValidated = new DelegateInArgument<TraceableRethrowActivity>();
            var validationContext = new DelegateInArgument<ValidationContext>();
            var found = new Variable<bool>();

            return new Constraint<TraceableRethrowActivity>
            {
                Body = new ActivityAction<TraceableRethrowActivity, ValidationContext>
                {
                    Argument1 = activityBeingValidated,
                    Argument2 = validationContext,
                    Handler = new Sequence
                    {
                        Variables = { found },
                        Activities =
                        {
                            new IsInsideTraceableTryCatchCatch
                            {
                                ParentChain = new GetParentChain
                                {
                                    ValidationContext = validationContext
                                },
                                Result = found
                            },
                            new AssertValidation
                            {
                                Assertion = new InArgument<bool>(found),
                                Message = new InArgument<string>(
                                    "Traceable Rethrow must be placed inside Traceable TryCatch → Catch.")
                            }
                        }
                    }
                }
            };
        }

        private sealed class IsInsideTraceableTryCatchCatch : CodeActivity<bool>
        {
            public InArgument<IEnumerable<Activity>> ParentChain { get; set; }

            protected override bool Execute(CodeActivityContext context)
            {
                List<Activity> parents = (ParentChain.Get(context) ?? Enumerable.Empty<Activity>()).ToList();
                foreach (Activity parent in parents)
                {
                    var tryCatch = parent as TraceableTryCatchActivity;
                    if (tryCatch?.Catch?.Handler == null)
                    {
                        continue;
                    }

                    if (parents.Contains(tryCatch.Catch.Handler))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
