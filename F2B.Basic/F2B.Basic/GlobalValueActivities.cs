using System;
using System.Activities;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(BasicSimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Global Value")]
    [Description("Read a value from F2B.Global for the current source workflow run. Expression: F2B.Global.Get(\"keyname\")")]
    public sealed class GetGlobalValueActivity : CodeActivity<object>, System.Activities.Presentation.IActivityTemplateFactory
    {
        public GetGlobalValueActivity()
        {
            DisplayName = "Get Global Value";
        }

        [RequiredArgument]
        [DisplayName("Key")]
        [Category("Input.A")]
        public InArgument<string> Key { get; set; }

        [DisplayName("Value")]
        [Category("Output")]
        public OutArgument<object> Value { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new GetGlobalValueActivity();
        }

        protected override object Execute(CodeActivityContext context)
        {
            object value = Global.Get(context, Key.Get(context));
            Value?.Set(context, value);
            return value;
        }
    }

    [Designer(typeof(BasicSimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Set Global Value")]
    [Description("Write a value into F2B.Global for the current source workflow run. Expression: F2B.Global.Set(\"keyname\", value)")]
    public sealed class SetGlobalValueActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public SetGlobalValueActivity()
        {
            DisplayName = "Set Global Value";
        }

        [RequiredArgument]
        [DisplayName("Key")]
        [Category("Input.A")]
        public InArgument<string> Key { get; set; }

        [RequiredArgument]
        [DisplayName("Value")]
        [Category("Input.A")]
        public InArgument<object> Value { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new SetGlobalValueActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            Global.Set(context, Key.Get(context), Value.Get(context));
        }
    }
}
