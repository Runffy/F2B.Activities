using System;
using System.Collections.Generic;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpDocumentLoadWaiter
    {
        internal static void Wait(CdpTabSession session, CdpDocumentWaitScope scope, int timeoutMs)
        {
            if (session == null)
            {
                throw new ArgumentNullException("session");
            }

            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            do
            {
                if (IsScopeComplete(session, scope))
                {
                    return;
                }

                if (timeoutMs <= 0)
                {
                    break;
                }

                Thread.Sleep(50);
            }
            while (DateTime.UtcNow < deadline);

            throw new BrowserException(
                scope == CdpDocumentWaitScope.AllDocuments
                    ? string.Format("Not all documents reached readyState 'complete' within {0} ms.", timeoutMs)
                    : string.Format("Main document did not reach readyState 'complete' within {0} ms.", timeoutMs));
        }

        private static bool IsScopeComplete(CdpTabSession session, CdpDocumentWaitScope scope)
        {
            if (!IsMainDocumentComplete(session))
            {
                return false;
            }

            if (scope == CdpDocumentWaitScope.MainDocument)
            {
                return true;
            }

            return AreAllFrameDocumentsComplete(session);
        }

        private static bool IsMainDocumentComplete(CdpTabSession session)
        {
            var readyState = session.TryGetMainReadyState();
            if (!string.Equals(readyState, "complete", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // loadEventFired can be missed (especially on history Back/Forward). Trust JS readyState.
            session.ClearLoadingFlagIfDocumentComplete();
            return true;
        }

        private static bool AreAllFrameDocumentsComplete(CdpTabSession session)
        {
            var frameTree = GetFrameTreeRoot(session);
            if (frameTree == null)
            {
                return false;
            }

            var frameIds = CdpFrameHelper.CollectAllFrameIds(frameTree);
            foreach (var frameId in frameIds)
            {
                string readyState;
                if (!TryGetFrameReadyState(session, frameId, out readyState) ||
                    !string.Equals(readyState, "complete", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return frameIds.Count > 0;
        }

        private static Dictionary<string, object> GetFrameTreeRoot(CdpTabSession session)
        {
            try
            {
                var treeResult = session.Send("Page.getFrameTree");
                return CdpValueConverter.GetDictionary(treeResult, "frameTree");
            }
            catch (BrowserException)
            {
                return null;
            }
        }

        private static bool TryGetFrameReadyState(CdpTabSession session, string frameId, out string readyState)
        {
            readyState = null;
            if (string.IsNullOrEmpty(frameId))
            {
                return false;
            }

            try
            {
                var contextId = CdpFrameHelper.TryCreateIsolatedWorld(session.Client, frameId, "f2b-doc-wait");
                if (!contextId.HasValue)
                {
                    return false;
                }

                var response = session.Send("Runtime.evaluate", new Dictionary<string, object>
                {
                    { "expression", "document.readyState" },
                    { "contextId", contextId.Value },
                    { "returnByValue", true }
                });

                var result = CdpValueConverter.GetDictionary(response, "result");
                readyState = result != null ? CdpValueConverter.GetString(result, "value") : null;
                return !string.IsNullOrEmpty(readyState);
            }
            catch (BrowserException)
            {
                return false;
            }
        }
    }
}
