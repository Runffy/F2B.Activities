using System;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeControllerMessageEventArgs : EventArgs
    {
        public BridgeControllerMessageEventArgs(string controllerId, string message)
        {
            ControllerId = controllerId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string ControllerId { get; }

        public string Message { get; }
    }
}
