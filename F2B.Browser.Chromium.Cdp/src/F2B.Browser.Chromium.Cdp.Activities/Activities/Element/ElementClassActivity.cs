using System;
using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-Class")]
    [Description("Check, add, or remove a CSS class on the target element.")]
    public sealed class ElementClassActivity : CdpElementTargetActivityBase
    {
        public ElementClassActivity()
            : base("Element-Class")
        {
        }

        [DisplayName("Name")]
        [Description("CSS class name.")]
        [RequiredArgument]
        [Category("Input.D")]
        public InArgument<string> Name { get; set; }

        [DisplayName("Type")]
        [Description("Class operation type.")]
        [Category("Input.D")]
        [DefaultValue(CdpClassOperationType.Exists)]
        [TypeConverter(typeof(CdpClassOperationTypeConverter))]
        public CdpClassOperationType Type { get; set; } = CdpClassOperationType.Exists;

        [DisplayName("Result")]
        [Description("Outputs whether the class exists when Type=Exists.")]
        [Category("Output")]
        public OutArgument<bool> Result { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var element = ResolveTargetElementWithTimeout(context, Timeout);
            var className = Name.Get(context);
            var escaped = className.Replace("\\", "\\\\").Replace("'", "\\'");

            switch (Type)
            {
                case CdpClassOperationType.Add:
                    element.RunJs("this.classList.add('" + escaped + "');");
                    break;
                case CdpClassOperationType.Remove:
                    element.RunJs("this.classList.remove('" + escaped + "');");
                    break;
                default:
                    var exists = element.RunJs("return this.classList.contains('" + escaped + "');");
                    Result?.Set(context, Convert.ToBoolean(exists));
                    break;
            }
        }
    }
}
