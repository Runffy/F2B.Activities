using System;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeClientMessageEventArgs : EventArgs
    {
        public BridgeClientMessageEventArgs(string instanceId, string message)
        {
            InstanceId = instanceId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string InstanceId { get; }

        public string Message { get; }
    }
}
