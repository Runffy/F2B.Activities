using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Diagnose Trident Host")]
    [Description("List all Internet Explorer_Server layers under the connected window and optionally probe getElementById. Writes to Console (OpenRPA log).")]
    public sealed class DiagnoseTridentHostActivity : CodeActivity
    {
        [DisplayName("IE Window")]
        [Category("Input")]
        public InArgument<EmbeddedIEWindow> IEWindow { get; set; }

        [DisplayName("Probe Element Id")]
        [Category("Input")]
        [Description("Optional. e.g. userName — prints HIT/miss per IE_Server document.")]
        public InArgument<string> ProbeElementId { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var window = IEWindow == null ? null : IEWindow.Get(context);
            if (window == null)
                throw new InvalidOperationException("IE Window is required.");

            var probe = ProbeElementId == null ? null : ProbeElementId.Get(context);
            TridentHostDiagnostics.DiagnoseTridentHost(window, probe);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            if (IEWindow == null || IEWindow.Expression == null)
                metadata.AddValidationError("IE Window is required.");
        }
    }
}
