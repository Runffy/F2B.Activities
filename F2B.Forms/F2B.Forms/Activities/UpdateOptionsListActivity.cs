using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Update Options List")]
    [Description("Update ComboBox options. Options is IEnumerable<string>.")]
    public sealed class UpdateOptionsListActivity : CodeActivity
    {
        public UpdateOptionsListActivity()
        {
            DisplayName = "Update Options List";
            ClearExisting = new InArgument<bool>(true);
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Options")]
        [Description("IEnumerable<string> of option texts.")]
        [Category("Input.A")]
        public InArgument<IEnumerable<string>> Options { get; set; }

        [DisplayName("Clear Existing")]
        [Category("Input.B")]
        public InArgument<bool> ClearExisting { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.UpdateOptionsList(
                ControlId.Get(context),
                Options.Get(context),
                ClearExisting.Get(context));
        }
    }
}
