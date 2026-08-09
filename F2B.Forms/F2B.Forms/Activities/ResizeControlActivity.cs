using System;
using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Resize Control")]
    [Description("Resize a control or the form at runtime. Use Control Id 'form' (or the form id) for the window. Leave Width/Height empty to keep that dimension.")]
    public sealed class ResizeControlActivity : CodeActivity
    {
        public ResizeControlActivity()
        {
            DisplayName = "Resize Control";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Description("Target control id, or 'form' / form id for the window.")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [DisplayName("Width")]
        [Description("New width in pixels. Empty / null / 0 = keep current.")]
        [Category("Input.A")]
        public InArgument<string> Width { get; set; }

        [DisplayName("Height")]
        [Description("New height in pixels. Empty / null / 0 = keep current.")]
        [Category("Input.A")]
        public InArgument<string> Height { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.ResizeControl(
                ControlId.Get(context),
                ParseDimension(Width.Get(context), "Width"),
                ParseDimension(Height.Get(context), "Height"));
        }

        internal static int? ParseDimension(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (!int.TryParse(value.Trim(), out int size))
            {
                throw new InvalidOperationException(
                    name + " must be a positive integer, or empty to keep current. Value: '" + value + "'.");
            }

            if (size <= 0)
            {
                return null;
            }

            return size;
        }
    }
}
