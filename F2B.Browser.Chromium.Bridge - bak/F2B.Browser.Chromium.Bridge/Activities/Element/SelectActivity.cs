using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Select")]
    [Description("Select specified options in the target select element.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class SelectActivity : BridgeElementTargetActivityBase
    {
        public SelectActivity() : base("Select") { }

        [DisplayName("Value Type")]
        [Category("Input.D")]
        [DefaultValue(BridgeSelectValType.Text)]
        [TypeConverter("F2B.Browser.Chromium.Bridge.BridgeSelectValTypeConverter, F2B.Browser.Chromium.Bridge")]
        public BridgeSelectValType ValType { get; set; } = BridgeSelectValType.Text;

        [DisplayName("Values")]
        [Category("Input.E")]
        public InArgument<string[]> Values { get; set; }

        [DisplayName("Texts")]
        [Category("Input.E")]
        public InArgument<string[]> Texts { get; set; }

        [DisplayName("Indices")]
        [Category("Input.E")]
        public InArgument<int[]> Indices { get; set; }

        [DisplayName("Validate Content After Selected")]
        [Category("Input.F")]
        [DefaultValue(false)]
        [TypeConverter("F2B.Browser.Chromium.Bridge.BridgeBooleanTypeConverter, F2B.Browser.Chromium.Bridge")]
        public bool ValidateContentAfterSelected { get; set; }

        [DisplayName("Wait Before Validate (ms)")]
        [Category("Input.F")]
        [DefaultValue(500)]
        public InArgument<int> Interval { get; set; } = 500;

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            string[] texts = null;
            string[] values = null;
            int[] indices = null;
            BridgeSelectValueType valType;

            switch (ValType)
            {
                case BridgeSelectValType.Value:
                    values = Values == null ? null : Values.Get(context);
                    valType = BridgeSelectValueType.Value;
                    break;
                case BridgeSelectValType.Index:
                    indices = Indices == null ? null : Indices.Get(context);
                    valType = BridgeSelectValueType.Index;
                    break;
                default:
                    texts = Texts == null ? null : Texts.Get(context);
                    valType = BridgeSelectValueType.Text;
                    break;
            }

            var budget = new TimeoutBudget(BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000));
            var target = ResolveTargetElement(context, budget.RemainingMs);
            if (budget.RemainingMs <= 0)
                throw new TimeoutException("Select timeout before operation.");

            target.Select(
                valType,
                texts,
                values,
                indices,
                ValidateContentAfterSelected,
                BridgeActivityArgumentHelper.GetOrDefault(Interval, context, 500),
                budget.RemainingMs);
        }
    }
}
