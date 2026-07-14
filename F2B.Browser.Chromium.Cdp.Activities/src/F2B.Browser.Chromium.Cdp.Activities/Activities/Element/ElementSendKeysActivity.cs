using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-SendKeys")]
    [Description("Send special keys or key chords. Tab/Frame with empty Selector targets the document body.")]
    public sealed class ElementSendKeysActivity : CdpElementTargetActivityBase
    {
        public ElementSendKeysActivity()
            : base("Element-SendKeys")
        {
        }

        protected override bool SendKeysBodySpecial
        {
            get { return true; }
        }

        [DisplayName("Keys")]
        [Description("Keys to send. Use CdpKey constants or string values.")]
        [RequiredArgument]
        [Category("Input.D")]
        public InArgument<object[]> Keys { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var element = ResolveTargetElementWithTimeout(context, Timeout);
            var keys = Keys.Get(context);
            element.SendKeys(CdpKeysParser.Parse(keys));
        }
    }
}
