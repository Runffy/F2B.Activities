using System;
using System.Collections.Generic;
using System.Linq;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;
using F2B.Browser.Chromium.Cdp.Selectors;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpFrameResolver
    {
        public static CdpFrame FindFrame(
            CdpTab tab,
            CdpFrame parentFrame,
            CdpElement parentElement,
            string selectorXml,
            int timeoutMs,
            bool throwException)
        {
            if (tab == null)
            {
                throw new ArgumentNullException("tab");
            }

            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            BrowserException lastError = null;

            do
            {
                try
                {
                    var frame = TryFindFrameOnce(tab, parentFrame, parentElement, selectorXml);
                    if (frame != null)
                    {
                        return frame;
                    }
                }
                catch (BrowserException ex)
                {
                    lastError = ex;
                }

                if (timeoutMs <= 0)
                {
                    break;
                }

                System.Threading.Thread.Sleep(100);
            }
            while (DateTime.UtcNow < deadline);

            if (!throwException)
            {
                return null;
            }

            if (lastError != null)
            {
                throw lastError;
            }

            throw new BrowserException(
                timeoutMs <= 0
                    ? "FindFrame failed: no matching frame."
                    : string.Format("FindFrame failed within {0} ms.", timeoutMs));
        }

        public static CdpFrame FromHostElement(CdpElement hostElement, CdpBase parent)
        {
            if (hostElement == null)
            {
                throw new ArgumentNullException("hostElement");
            }

            var tag = (hostElement.Tag ?? string.Empty).ToLowerInvariant();
            if (tag != "iframe" && tag != "frame")
            {
                throw new BrowserException("AsFrame requires an iframe or frame element.");
            }

            string frameId;
            if (!TryGetContentFrameId(hostElement, out frameId) || string.IsNullOrEmpty(frameId))
            {
                throw new BrowserException("Unable to resolve frame id for the host element.");
            }

            return new CdpFrame(hostElement.Tab, frameId, hostElement, parent ?? hostElement.Tab);
        }

        public static CdpElement ResolveOwnerElement(CdpTab tab, string frameId)
        {
            if (tab == null || string.IsNullOrEmpty(frameId))
            {
                return null;
            }

            var session = tab.GetSession();
            try
            {
                var owner = session.Send("DOM.getFrameOwner", new Dictionary<string, object>
                {
                    { "frameId", frameId }
                });

                var backendNodeId = CdpValueConverter.GetInt(owner, "backendNodeId");
                var nodeId = CdpValueConverter.GetInt(owner, "nodeId");
                if (backendNodeId <= 0)
                {
                    return null;
                }

                if (nodeId <= 0)
                {
                    var request = session.Send("DOM.requestNode", new Dictionary<string, object>
                    {
                        { "backendNodeId", backendNodeId }
                    });
                    nodeId = CdpValueConverter.GetInt(request, "nodeId");
                }

                var describe = session.Send("DOM.describeNode", new Dictionary<string, object>
                {
                    { "backendNodeId", backendNodeId }
                });
                var node = CdpValueConverter.GetDictionary(describe, "node");
                var tag = CdpValueConverter.GetString(node, "localName") ?? "iframe";
                var objectId = ResolveObjectId(session, backendNodeId, nodeId);
                return new CdpElement(tab, tag, backendNodeId, nodeId, objectId);
            }
            catch
            {
                return null;
            }
        }

        private static CdpFrame TryFindFrameOnce(
            CdpTab tab,
            CdpFrame parentFrame,
            CdpElement parentElement,
            string selectorXml)
        {
            if (string.IsNullOrWhiteSpace(selectorXml))
            {
                throw new ArgumentException("Selector is required for FindFrame.");
            }

            if (parentElement != null)
            {
                var host = SelectorElementFinder.FindElement(parentElement, selectorXml, 0, false);
                if (host == null)
                {
                    return null;
                }

                return FromHostElement(host, parentElement);
            }

            var scope = SelectorXmlSerializer.SplitScopeForOperation(selectorXml);
            var relativeFrameLevels = scope.FrameLevels != null
                ? new List<SelectorLevel>(scope.FrameLevels)
                : new List<SelectorLevel>();

            if (relativeFrameLevels.Count == 0 && scope.ElementLevels != null && scope.ElementLevels.Count > 0)
            {
                var host = parentFrame != null
                    ? SelectorElementFinder.FindElement(parentFrame, selectorXml, 0, false)
                    : SelectorElementFinder.FindElement(tab, selectorXml, 0, false);
                if (host == null)
                {
                    return null;
                }

                return FromHostElement(host, (CdpBase)parentFrame ?? tab);
            }

            if (relativeFrameLevels.Count == 0)
            {
                return null;
            }

            string frameId;
            var levelsFromTab = parentFrame != null && parentFrame.FrameLevelsFromTab != null
                ? parentFrame.FrameLevelsFromTab
                : null;
            if (levelsFromTab != null && levelsFromTab.Count > 0)
            {
                var frameLevels = new List<SelectorLevel>(levelsFromTab);
                frameLevels.AddRange(relativeFrameLevels);
                if (!TryMatchFrameId(tab, frameLevels, out frameId))
                {
                    return null;
                }
            }
            else if (parentFrame != null && !string.IsNullOrEmpty(parentFrame.FrameId))
            {
                // Frame from Element/AsFrame has no selector path; match relative levels under its CDP frame.
                if (!TryMatchFrameIdUnderParent(tab, parentFrame.FrameId, relativeFrameLevels, out frameId))
                {
                    return null;
                }
            }
            else if (!TryMatchFrameId(tab, relativeFrameLevels, out frameId))
            {
                return null;
            }

            var owner = ResolveOwnerElement(tab, frameId);
            if (owner == null)
            {
                return null;
            }

            var absoluteLevels = new List<SelectorLevel>();
            if (levelsFromTab != null && levelsFromTab.Count > 0)
            {
                absoluteLevels.AddRange(levelsFromTab);
            }

            absoluteLevels.AddRange(relativeFrameLevels);

            CdpBase parent = tab;
            if (absoluteLevels.Count > 1)
            {
                var parentLevels = absoluteLevels.Take(absoluteLevels.Count - 1).ToList();
                string parentFrameId;
                if (TryMatchFrameId(tab, parentLevels, out parentFrameId))
                {
                    var parentOwner = ResolveOwnerElement(tab, parentFrameId);
                    parent = new CdpFrame(tab, parentFrameId, parentOwner, tab, parentLevels);
                }
            }
            else if (parentFrame != null)
            {
                parent = parentFrame;
            }

            return new CdpFrame(tab, frameId, owner, parent, absoluteLevels);
        }

        private static bool TryMatchFrameId(CdpTab tab, IList<SelectorLevel> frameLevels, out string frameId)
        {
            return TryMatchFrameIdFromNode(tab, null, frameLevels, out frameId);
        }

        private static bool TryMatchFrameIdUnderParent(
            CdpTab tab,
            string parentFrameId,
            IList<SelectorLevel> frameLevels,
            out string frameId)
        {
            return TryMatchFrameIdFromNode(tab, parentFrameId, frameLevels, out frameId);
        }

        private static bool TryMatchFrameIdFromNode(
            CdpTab tab,
            string startFrameId,
            IList<SelectorLevel> frameLevels,
            out string frameId)
        {
            frameId = null;
            if (tab == null || frameLevels == null || frameLevels.Count == 0)
            {
                return false;
            }

            var session = tab.GetSession();
            var treeResult = session.Send("Page.getFrameTree");
            var frameTree = CdpValueConverter.GetDictionary(treeResult, "frameTree");
            if (frameTree == null)
            {
                return false;
            }

            var currentNode = frameTree;
            if (!string.IsNullOrEmpty(startFrameId))
            {
                currentNode = CdpFrameHelper.FindFrameNode(frameTree, startFrameId);
                if (currentNode == null)
                {
                    return false;
                }
            }

            CdpClient client = session.Client;
            string targetFrameId = null;

            foreach (var frmLevel in frameLevels)
            {
                var childFrames = CdpFrameHelper.CollectDirectChildFrames(currentNode);
                var matchedFrame = CdpFrameHelper.MatchFrame(client, childFrames, frmLevel);
                if (matchedFrame == null)
                {
                    return false;
                }

                targetFrameId = CdpValueConverter.GetString(matchedFrame, "id");
                if (string.IsNullOrEmpty(targetFrameId))
                {
                    return false;
                }

                currentNode = CdpFrameHelper.FindDirectChildFrameNode(currentNode, targetFrameId);
                if (currentNode == null)
                {
                    return false;
                }
            }

            frameId = targetFrameId;
            return !string.IsNullOrEmpty(frameId);
        }

        private static bool TryGetContentFrameId(CdpElement hostElement, out string frameId)
        {
            frameId = null;
            try
            {
                hostElement.Context.RefreshIds();
                var session = hostElement.Tab.GetSession();
                var describe = session.Send("DOM.describeNode", new Dictionary<string, object>
                {
                    { "backendNodeId", hostElement.BackendNodeId },
                    { "depth", 0 },
                    { "pierce", true }
                });

                var node = CdpValueConverter.GetDictionary(describe, "node");
                if (node == null)
                {
                    return false;
                }

                frameId = CdpValueConverter.GetString(node, "frameId");
                if (!string.IsNullOrEmpty(frameId))
                {
                    return true;
                }

                // Some Chrome versions expose content frame via contentDocument / frameId on nested fields.
                var contentDocument = CdpValueConverter.GetDictionary(node, "contentDocument");
                if (contentDocument != null)
                {
                    frameId = CdpValueConverter.GetString(contentDocument, "frameId");
                    if (!string.IsNullOrEmpty(frameId))
                    {
                        return true;
                    }
                }

                // Reverse lookup via frame tree + getFrameOwner.
                return TryFindFrameIdByOwnerBackendId(hostElement.Tab, hostElement.BackendNodeId, out frameId);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryFindFrameIdByOwnerBackendId(CdpTab tab, int backendNodeId, out string frameId)
        {
            frameId = null;
            try
            {
                var session = tab.GetSession();
                var treeResult = session.Send("Page.getFrameTree");
                var frameTree = CdpValueConverter.GetDictionary(treeResult, "frameTree");
                var ids = CdpFrameHelper.CollectAllFrameIds(frameTree);
                foreach (var candidate in ids)
                {
                    try
                    {
                        var owner = session.Send("DOM.getFrameOwner", new Dictionary<string, object>
                        {
                            { "frameId", candidate }
                        });
                        if (CdpValueConverter.GetInt(owner, "backendNodeId") == backendNodeId)
                        {
                            frameId = candidate;
                            return true;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static string ResolveObjectId(CdpTabSession session, int backendNodeId, int nodeId)
        {
            try
            {
                if (nodeId > 0)
                {
                    var resolved = session.Send("DOM.resolveNode", new Dictionary<string, object>
                    {
                        { "nodeId", nodeId }
                    });
                    var obj = CdpValueConverter.GetDictionary(resolved, "object");
                    var objectId = obj != null ? CdpValueConverter.GetString(obj, "objectId") : null;
                    if (!string.IsNullOrEmpty(objectId))
                    {
                        return objectId;
                    }
                }

                var byBackend = session.Send("DOM.resolveNode", new Dictionary<string, object>
                {
                    { "backendNodeId", backendNodeId }
                });
                var backendObj = CdpValueConverter.GetDictionary(byBackend, "object");
                return backendObj != null ? CdpValueConverter.GetString(backendObj, "objectId") : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
