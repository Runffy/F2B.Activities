using System;

namespace F2B.Browser.Chromium.Cdp.Browser
{
    [Flags]
    public enum CdpNetworkMethod
    {
        None = 0,
        Get = 1 << 0,
        Post = 1 << 1,
        Put = 1 << 2,
        Delete = 1 << 3,
        Patch = 1 << 4,
        Head = 1 << 5,
        Options = 1 << 6,
        All = 1 << 30
    }

    [Flags]
    public enum CdpNetworkResourceType
    {
        None = 0,
        Document = 1 << 0,
        Stylesheet = 1 << 1,
        Image = 1 << 2,
        Media = 1 << 3,
        Font = 1 << 4,
        Script = 1 << 5,
        TextTrack = 1 << 6,
        Xhr = 1 << 7,
        Fetch = 1 << 8,
        Prefetch = 1 << 9,
        EventSource = 1 << 10,
        WebSocket = 1 << 11,
        Manifest = 1 << 12,
        SignedExchange = 1 << 13,
        Ping = 1 << 14,
        CspViolationReport = 1 << 15,
        Preflight = 1 << 16,
        Other = 1 << 17,
        All = 1 << 30
    }
}
