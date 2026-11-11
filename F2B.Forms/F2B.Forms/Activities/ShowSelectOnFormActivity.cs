using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("Dialog")]
    [DisplayName("Show Select On Form")]
    [Description("Modal popup on the active form: message + dropdown + button. Blocks until OK or close (X → Selected Item null). Allow Null=false throws on null.")]
    public sealed class ShowSelectOnFormActivity : CodeActivity<object>
    {
        public ShowSelectOnFormActivity()
        {
            DisplayName = "Show Select On Form";
            ButtonName = new InArgument<string>("OK");
            AllowNull = new InArgument<bool>(true);
        }

        [DisplayName("Title")]
        [Category("Input.A")]
        public InArgument<string> Title { get; set; }

        [RequiredArgument]
        [DisplayName("Message")]
        [Category("Input.A")]
        public InArgument<string> Message { get; set; }

        [DisplayName("Options")]
        [Description("Dropdown items: array/list, or a single value. Null/empty = no items. Default null.")]
        [Category("Input.B")]
        public InArgument<object> Options { get; set; }

        [DisplayName("Button Name")]
        [Description("Confirm button caption. Default: OK.")]
        [Category("Input.B")]
        public InArgument<string> ButtonName { get; set; }

        [DisplayName("Allow Null")]
        [Description("When false, null selection (no pick / null option / close X) throws. Default: true.")]
        [Category("Input.C")]
        public InArgument<bool> AllowNull { get; set; }

        [DisplayName("Selected Item")]
        [Category("Output")]
        public OutArgument<object> SelectedItem { get; set; }

        protected override object Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            object selected = session.ShowSelectPopup(
                Title == null ? null : Title.Get(context),
                Message.Get(context),
                Options == null ? null : Options.Get(context),
                ButtonName == null ? null : ButtonName.Get(context),
                AllowNull.Get(context));
            SelectedItem?.Set(context, selected);
            return selected;
        }
    }
}
