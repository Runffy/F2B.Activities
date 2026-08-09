using System;
using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Set Control Position")]
    [Description("Move a control using X/Y relative to its parent container. Leave X or Y empty to keep that coordinate.")]
    public sealed class SetControlPositionActivity : CodeActivity
    {
        public SetControlPositionActivity()
        {
            DisplayName = "Set Control Position";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [DisplayName("X")]
        [Description("Left offset relative to the parent. Empty = keep current.")]
        [Category("Input.A")]
        public InArgument<string> X { get; set; }

        [DisplayName("Y")]
        [Description("Top offset relative to the parent. Empty = keep current.")]
        [Category("Input.A")]
        public InArgument<string> Y { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.SetControlPosition(
                ControlId.Get(context),
                ParseOptionalCoordinate(X.Get(context), "X"),
                ParseOptionalCoordinate(Y.Get(context), "Y"));
        }

        internal static int? ParseOptionalCoordinate(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (!int.TryParse(value.Trim(), out int coordinate))
            {
                throw new InvalidOperationException(
                    name + " must be an integer, or empty to keep current. Value: '" + value + "'.");
            }

            return coordinate;
        }
    }
}
