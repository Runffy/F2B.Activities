using System;
using FlaUI.Core;
using FlaUI.UIA3;

namespace FlaUI.Inspector.Services
{
    public sealed class AutomationContext : IDisposable
    {
        private static readonly Lazy<AutomationContext> InstanceLazy = new Lazy<AutomationContext>(() => new AutomationContext());

        private UIA3Automation _automation;

        private AutomationContext()
        {
            _automation = new UIA3Automation();
        }

        public static AutomationContext Instance => InstanceLazy.Value;

        public UIA3Automation Automation => _automation ?? throw new ObjectDisposedException(nameof(AutomationContext));

        public void Dispose()
        {
            _automation?.Dispose();
            _automation = null;
        }
    }
}
