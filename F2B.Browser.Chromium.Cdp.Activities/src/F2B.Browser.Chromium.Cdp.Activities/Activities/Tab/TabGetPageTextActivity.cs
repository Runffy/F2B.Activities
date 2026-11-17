using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    /// <summary>
    /// Copies whole-page text via CDP Ctrl+A / Ctrl+C on the tab document body,
    /// using a clipboard UUID marker to detect success and retry.
    /// </summary>
    [ToolboxPath("Tab")]
    [DisplayName("GetPageText")]
    [Description("Copy the whole page text with Ctrl+A/Ctrl+C via CDP (Tab only). Sets a UUID clipboard marker first; retries while clipboard still equals the marker.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class TabGetPageTextActivity : CodeActivity
    {
        public TabGetPageTextActivity()
        {
            DisplayName = "GetPageText";
        }

        [DisplayName("Tab")]
        [Description("Tab whose page text will be copied. Optional when Selector contains <wnd>.")]
        [Category("Input.A")]
        public InArgument<CdpTab> Tab { get; set; }

        [DisplayName("Selector")]
        [Description("Optional selector XML with <wnd> used to locate the tab.")]
        [Category("Input.A")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Activate Tab")]
        [Description("Activate the tab before sending Ctrl+A/Ctrl+C.")]
        [Category("Input.C")]
        [DefaultValue(true)]
        public InArgument<bool> ActivateTab { get; set; } = true;

        [DisplayName("Retry Count")]
        [Description("How many Ctrl+A/Ctrl+C attempts when clipboard still equals the marker.")]
        [Category("Input.C")]
        [DefaultValue(3)]
        public InArgument<int> RetryCount { get; set; } = 3;

        [DisplayName("Retry Interval (ms)")]
        [Description("Wait between full copy attempts.")]
        [Category("Input.C")]
        [DefaultValue(200)]
        public InArgument<int> RetryInterval { get; set; } = 200;

        [DisplayName("Clipboard Wait (ms)")]
        [Description("Per attempt, poll clipboard for this long waiting for content to leave the marker.")]
        [Category("Input.C")]
        [DefaultValue(500)]
        public InArgument<int> ClipboardWait { get; set; } = 500;

        [DisplayName("Delay Before (ms)")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [DisplayName("Timeout (ms)")]
        [Description("Timeout used when resolving document body.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Text")]
        [Description("Clipboard text after a successful Ctrl+C (content differs from the marker).")]
        [Category("Output")]
        public OutArgument<string> Text { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var retryCount = Math.Max(1, CdpActivityArgumentHelper.GetOrDefault(RetryCount, context, 3));
            var retryInterval = Math.Max(0, CdpActivityArgumentHelper.GetOrDefault(RetryInterval, context, 200));
            var clipboardWait = Math.Max(0, CdpActivityArgumentHelper.GetOrDefault(ClipboardWait, context, 500));
            var activateTab = CdpActivityArgumentHelper.GetOrDefault(ActivateTab, context, true);
            var timeoutMs = CdpActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            var delayBefore = CdpActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300);

            var selector = Selector == null ? null : Selector.Get(context);
            var inputTab = Tab == null ? null : Tab.Get(context);
            var tab = CdpTabLocator.Resolve(selector, inputTab);

            CdpDelay.Apply(delayBefore);

            if (activateTab)
            {
                tab.Browser.ActivateTab(tab);
            }

            // Ctrl+A selects the whole page; always send keys against document body.
            var body = tab.FindElement("<ctrl tag=\"body\" />", timeoutMs, true);

            for (var attempt = 1; attempt <= retryCount; attempt++)
            {
                var marker = "F2B.Cdp.GetPageText:" + Guid.NewGuid().ToString("N");
                CdpClipboardHelper.SetText(marker);

                body.SendKeys(CdpKey.CtrlA, CdpKey.CtrlC);

                var deadline = DateTime.UtcNow.AddMilliseconds(clipboardWait);
                while (true)
                {
                    var clipboard = CdpClipboardHelper.GetText();
                    if (!string.Equals(clipboard, marker, StringComparison.Ordinal))
                    {
                        Text?.Set(context, clipboard ?? string.Empty);
                        return;
                    }

                    if (DateTime.UtcNow >= deadline)
                    {
                        break;
                    }

                    Thread.Sleep(50);
                }

                if (attempt < retryCount && retryInterval > 0)
                {
                    Thread.Sleep(retryInterval);
                }
            }

            throw new InvalidOperationException(
                "GetPageText failed: clipboard still equals the marker after Ctrl+A/Ctrl+C retries. " +
                "Ensure the browser tab can receive CDP key events and that copy is not blocked.");
        }
    }
}
