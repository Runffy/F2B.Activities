using System;
using System.Activities;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    public enum NumberOperation
    {
        Increment,
        Decrement
    }

    public enum NumberValueType
    {
        Int,
        Double
    }

    [Designer(typeof(NumberIncrementDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Number Increment")]
    [Description("Increment or decrement a workflow numeric variable by a configurable step.")]
    public sealed class NumberIncrementActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public NumberIncrementActivity()
        {
            DisplayName = "Number Increment";
            Operation = NumberOperation.Increment;
            NumberType = NumberValueType.Int;
            IntStep = new InArgument<int>(1);
            DoubleStep = new InArgument<double>(1d);
        }

        [DisplayName("Operation")]
        [Category("Input.A")]
        [DefaultValue(NumberOperation.Increment)]
        public NumberOperation Operation { get; set; }

        [DisplayName("Number Type")]
        [Category("Input.A")]
        [DefaultValue(NumberValueType.Int)]
        public NumberValueType NumberType { get; set; }

        [DisplayName("Int Variable")]
        [Description("In/out integer workflow variable. Used when Number Type is Int.")]
        [Category("Input.B")]
        public InOutArgument<int> IntVariable { get; set; }

        [DisplayName("Int Step")]
        [Category("Input.B")]
        [DefaultValue(1)]
        public InArgument<int> IntStep { get; set; }

        [DisplayName("Double Variable")]
        [Description("In/out double workflow variable. Used when Number Type is Double.")]
        [Category("Input.C")]
        public InOutArgument<double> DoubleVariable { get; set; }

        [DisplayName("Double Step")]
        [Category("Input.C")]
        [DefaultValue(1d)]
        public InArgument<double> DoubleStep { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new NumberIncrementActivity
            {
                Operation = NumberOperation.Increment,
                NumberType = NumberValueType.Int,
                IntStep = new InArgument<int>(1),
                DoubleStep = new InArgument<double>(1d),
                DisplayName = "Number Increment"
            };
        }

        protected override void Execute(CodeActivityContext context)
        {
            bool increment = Operation != NumberOperation.Decrement;

            if (NumberType == NumberValueType.Double)
            {
                if (DoubleVariable == null)
                {
                    throw new InvalidOperationException("Double Variable is required when Number Type is Double.");
                }

                double value = DoubleVariable.Get(context);
                double step = DoubleStep == null ? 1d : DoubleStep.Get(context);
                DoubleVariable.Set(context, increment ? value + step : value - step);
                return;
            }

            if (IntVariable == null)
            {
                throw new InvalidOperationException("Int Variable is required when Number Type is Int.");
            }

            int intValue = IntVariable.Get(context);
            int intStep = IntStep == null ? 1 : IntStep.Get(context);
            IntVariable.Set(context, increment ? intValue + intStep : intValue - intStep);
        }

        internal static string GetDisplayNameFor(NumberOperation operation)
        {
            return operation == NumberOperation.Decrement ? "Number Decrement" : "Number Increment";
        }
    }
}
