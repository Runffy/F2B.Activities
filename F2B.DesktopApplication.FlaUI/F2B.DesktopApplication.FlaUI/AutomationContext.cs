using System;
using FlaUI.UIA3;

namespace F2B.DesktopApplication.FlaUI
{
    public sealed class AutomationContext : IDisposable
    {
        private static readonly Lazy<AutomationContext> LazyInstance = new Lazy<AutomationContext>(() => new AutomationContext());

        private UIA3Automation _automation;

        private AutomationContext()
        {
            _automation = new UIA3Automation();
        }

        public static AutomationContext Instance => LazyInstance.Value;

        public UIA3Automation Automation => _automation ?? throw new ObjectDisposedException(nameof(AutomationContext));

        public void Dispose()
        {
            _automation?.Dispose();
            _automation = null;
        }
    }
}
