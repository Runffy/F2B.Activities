using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace F2B.Browser.Chromium.Bridge
{
    public interface IBridgeRpcChannel : IDisposable
    {
        Task<BridgeRpcResponse> InvokeAsync(
            string action,
            string instanceId,
            IDictionary<string, object> parameters = null,
            int timeoutMs = 15000,
            CancellationToken cancellationToken = default);
    }
}
