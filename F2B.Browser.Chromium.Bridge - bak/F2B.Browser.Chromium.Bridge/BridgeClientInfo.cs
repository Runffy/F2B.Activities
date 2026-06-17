using System;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeClientInfo
    {
        public BridgeClientInfo(string instanceId, string label, DateTime connectedAt)
        {
            InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
            Label = label ?? string.Empty;
            ConnectedAt = connectedAt;
        }

        public string InstanceId { get; }

        public string Label { get; }

        public DateTime ConnectedAt { get; }

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(Label) ? InstanceId : Label + " (" + InstanceId + ")"; }
        }
    }
}
