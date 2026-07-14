using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;
using F2B.Browser.Chromium.Cdp.Selectors;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    /// <summary>
    /// DOM operation context for a tab or an iframe chain (DrissionPage-style inner/cross-domain frame handling).
    /// </summary>
    internal sealed class CdpDomContext : IDisposable
    {
        private CdpDomContext(CdpTab tab, CdpClient client, bool ownsClient, int? executionContextId, bool isDiffDomain)
        {
            Tab = tab;
            Client = client;
            _ownsClient = ownsClient;
            ExecutionContextId = executionContextId;
            IsDiffDomain = isDiffDomain;
        }

        public CdpTab Tab { get; private set; }

        public CdpClient Client { get; private set; }

        public int? ExecutionContextId { get; private set; }

        public bool IsDiffDomain { get; private set; }

        public bool HasAlert
        {
            get { return Client != null && Client.AlertFlag; }
        }

        public static CdpDomContext ForTab(CdpTabSession tabSession)
        {
            return new CdpDomContext(tabSession.Tab, tabSession.Client, false, null, false);
        }

        public static CdpDomContext ForFrameId(CdpTab tab, CdpTabSession tabSession, string frameId)
        {
            if (tab == null)
            {
                throw new ArgumentNullException("tab");
            }

            if (tabSession == null)
            {
                throw new ArgumentNullException("tabSession");
            }

            if (string.IsNullOrEmpty(frameId))
            {
                throw new ArgumentException("frameId is required.", "frameId");
            }

            var contextId = CdpFrameHelper.TryCreateIsolatedWorld(tabSession.Client, frameId, "f2b-frame");
            if (!contextId.HasValue)
            {
                // Cross-domain: attach to frame target websocket.
                var wsUrl = CdpJsonClient.ResolveTargetWebSocketUrl(tab.Browser.Port, frameId);
                var client = CreateFrameClient(wsUrl);
                return new CdpDomContext(tab, client, true, null, true);
            }

            return new CdpDomContext(tab, tabSession.Client, false, contextId, false);
        }

        public static CdpDomContext ForFrameLevels(CdpTab tab, CdpTabSession tabSession, IList<SelectorLevel> frameLevels)
        {
            if (frameLevels == null || frameLevels.Count == 0)
            {
                return ForTab(tabSession);
            }

            var treeResult = tabSession.Send("Page.getFrameTree");
            var frameTree = CdpValueConverter.GetDictionary(treeResult, "frameTree");
            if (frameTree == null)
            {
                throw new BrowserException("Unable to read frame tree.");
            }

            var currentNode = frameTree;
            CdpClient client = tabSession.Client;
            var ownsClient = false;
            string targetFrameId = null;

            foreach (var frmLevel in frameLevels)
            {
                if (ownsClient)
                {
                    var localTree = client.Send("Page.getFrameTree");
                    currentNode = CdpValueConverter.GetDictionary(localTree, "frameTree");
                }

                var childFrames = CdpFrameHelper.CollectDirectChildFrames(currentNode);
                var matchedFrame = CdpFrameHelper.MatchFrame(client, childFrames, frmLevel);
                if (matchedFrame == null)
                {
                    throw new BrowserException("Frame selector did not match any iframe.");
                }

                targetFrameId = CdpValueConverter.GetString(matchedFrame, "id");
                if (string.IsNullOrEmpty(targetFrameId))
                {
                    throw new BrowserException("Matched frame has no id.");
                }

                if (!ownsClient)
                {
                    currentNode = CdpFrameHelper.FindDirectChildFrameNode(currentNode, targetFrameId);
                    if (currentNode == null)
                    {
                        throw new BrowserException("Frame tree node not found.");
                    }

                    continue;
                }

                if (CdpFrameHelper.TryGetInnerFrameDocumentBackendId(client, targetFrameId, out _))
                {
                    currentNode = CdpFrameHelper.FindDirectChildFrameNode(currentNode, targetFrameId);
                    if (currentNode == null)
                    {
                        throw new BrowserException("Frame tree node not found.");
                    }

                    continue;
                }

                client.Dispose();

                var wsUrl = CdpJsonClient.ResolveTargetWebSocketUrl(tab.Browser.Port, targetFrameId);
                client = CreateFrameClient(wsUrl);
                ownsClient = true;
                currentNode = CdpFrameHelper.FindDirectChildFrameNode(currentNode, targetFrameId);
            }

            int? contextId = null;
            var isDiffDomain = ownsClient;
            if (!string.IsNullOrEmpty(targetFrameId))
            {
                var evalClient = ownsClient ? client : tabSession.Client;
                contextId = CdpFrameHelper.TryCreateIsolatedWorld(evalClient, targetFrameId, "f2b-finder");
                if (contextId.HasValue)
                {
                    isDiffDomain = false;
                }
            }

            return new CdpDomContext(tab, client, ownsClient, contextId, isDiffDomain);
        }

        public Dictionary<string, object> Send(string method, Dictionary<string, object> parameters = null)
        {
            return Client.Send(method, parameters);
        }

        public string EvaluateString(string expression)
        {
            var result = Evaluate(expression);
            return result == null ? string.Empty : Convert.ToString(result);
        }

        public object Evaluate(string expression)
        {
            var parameters = new Dictionary<string, object>
            {
                { "expression", expression },
                { "returnByValue", true }
            };

            if (ExecutionContextId.HasValue)
            {
                parameters["contextId"] = ExecutionContextId.Value;
            }

            var response = Send("Runtime.evaluate", parameters);
            var inner = CdpValueConverter.GetDictionary(response, "result");
            if (inner == null)
            {
                return null;
            }

            object exceptionDetails;
            if (inner.TryGetValue("exceptionDetails", out exceptionDetails) && exceptionDetails != null)
            {
                throw new BrowserException(string.Format("JavaScript evaluation failed: {0}", exceptionDetails));
            }

            object value;
            return inner.TryGetValue("value", out value) ? value : null;
        }

        public string EvaluateObjectId(string expression)
        {
            var parameters = new Dictionary<string, object>
            {
                { "expression", expression },
                { "returnByValue", false }
            };

            if (ExecutionContextId.HasValue)
            {
                parameters["contextId"] = ExecutionContextId.Value;
            }

            var response = Send("Runtime.evaluate", parameters);
            var inner = CdpValueConverter.GetDictionary(response, "result");
            return inner != null ? CdpValueConverter.GetString(inner, "objectId") : null;
        }

        public CdpElement ResolveElement(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return null;
            }

            var request = Send("DOM.requestNode", new Dictionary<string, object>
            {
                { "objectId", objectId }
            });

            var nodeId = CdpValueConverter.GetInt(request, "nodeId");
            var describe = Send("DOM.describeNode", new Dictionary<string, object>
            {
                { "nodeId", nodeId }
            });

            var node = CdpValueConverter.GetDictionary(describe, "node");
            var tag = CdpValueConverter.GetString(node, "localName") ?? string.Empty;
            var backendNodeId = CdpValueConverter.GetInt(node, "backendNodeId");

            return new CdpElement(Tab, tag, backendNodeId, nodeId, objectId);
        }

        public void Dispose()
        {
            if (_ownsClient && Client != null)
            {
                Client.Dispose();
            }
        }

        private static CdpClient CreateFrameClient(string webSocketUrl)
        {
            var client = new CdpClient(webSocketUrl);
            client.Start();
            client.Enable("Page", "Runtime", "DOM", "Network");
            return client;
        }

        private readonly bool _ownsClient;
    }

    internal static class CdpFrameHelper
    {
        public static bool TryGetInnerFrameDocumentBackendId(CdpTabSession tabSession, string frameId, out int backendNodeId)
        {
            return TryGetInnerFrameDocumentBackendId(tabSession.Client, frameId, out backendNodeId);
        }

        public static bool TryGetInnerFrameDocumentBackendId(CdpClient client, string frameId, out int backendNodeId)
        {
            backendNodeId = 0;
            if (client == null || string.IsNullOrEmpty(frameId))
            {
                return false;
            }

            try
            {
                var owner = client.Send("DOM.getFrameOwner", new Dictionary<string, object>
                {
                    { "frameId", frameId }
                });

                var iframeBackendNodeId = CdpValueConverter.GetInt(owner, "backendNodeId");
                if (iframeBackendNodeId <= 0)
                {
                    return false;
                }

                var describe = client.Send("DOM.describeNode", new Dictionary<string, object>
                {
                    { "backendNodeId", iframeBackendNodeId }
                });

                var node = CdpValueConverter.GetDictionary(describe, "node");
                var contentDocument = CdpValueConverter.GetDictionary(node, "contentDocument");
                if (contentDocument == null)
                {
                    return false;
                }

                backendNodeId = CdpValueConverter.GetInt(contentDocument, "backendNodeId");
                return backendNodeId > 0;
            }
            catch
            {
                return false;
            }
        }

        public static int? TryCreateIsolatedWorld(CdpClient client, string frameId, string worldName)
        {
            if (client == null || string.IsNullOrEmpty(frameId))
            {
                return null;
            }

            try
            {
                var world = client.Send("Page.createIsolatedWorld", new Dictionary<string, object>
                {
                    { "frameId", frameId },
                    { "worldName", worldName ?? "f2b-world" }
                });

                var executionContextId = CdpValueConverter.GetInt(world, "executionContextId", -1);
                return executionContextId >= 0 ? (int?)executionContextId : null;
            }
            catch
            {
                return null;
            }
        }

        public static IList<Dictionary<string, object>> CollectDirectChildFrames(Dictionary<string, object> frameTreeNode)
        {
            var result = new List<Dictionary<string, object>>();
            if (frameTreeNode == null)
            {
                return result;
            }

            var childFrames = CdpValueConverter.GetList(frameTreeNode, "childFrames");
            if (childFrames == null)
            {
                return result;
            }

            foreach (var childEntry in childFrames)
            {
                var childNode = childEntry as Dictionary<string, object>;
                var frame = childNode != null ? CdpValueConverter.GetDictionary(childNode, "frame") : null;
                if (frame != null)
                {
                    result.Add(frame);
                }
            }

            return result;
        }

        public static IList<string> CollectAllFrameIds(Dictionary<string, object> frameTreeNode)
        {
            var ids = new List<string>();
            CollectAllFrameIdsCore(frameTreeNode, ids);
            return ids;
        }

        private static void CollectAllFrameIdsCore(Dictionary<string, object> frameTreeNode, IList<string> ids)
        {
            if (frameTreeNode == null)
            {
                return;
            }

            var frame = CdpValueConverter.GetDictionary(frameTreeNode, "frame");
            var frameId = frame != null ? CdpValueConverter.GetString(frame, "id") : null;
            if (!string.IsNullOrEmpty(frameId))
            {
                ids.Add(frameId);
            }

            var childFrames = CdpValueConverter.GetList(frameTreeNode, "childFrames");
            if (childFrames == null)
            {
                return;
            }

            foreach (var childEntry in childFrames)
            {
                CollectAllFrameIdsCore(childEntry as Dictionary<string, object>, ids);
            }
        }

        public static Dictionary<string, object> FindDirectChildFrameNode(Dictionary<string, object> frameTreeNode, string frameId)
        {
            if (frameTreeNode == null || string.IsNullOrEmpty(frameId))
            {
                return null;
            }

            var childFrames = CdpValueConverter.GetList(frameTreeNode, "childFrames");
            if (childFrames == null)
            {
                return null;
            }

            foreach (var childEntry in childFrames)
            {
                var childNode = childEntry as Dictionary<string, object>;
                var frame = childNode != null ? CdpValueConverter.GetDictionary(childNode, "frame") : null;
                if (frame != null && CdpValueConverter.GetString(frame, "id") == frameId)
                {
                    return childNode;
                }
            }

            return null;
        }

        /// <summary>Finds a frame-tree node by frame id anywhere under <paramref name="frameTreeNode"/> (inclusive).</summary>
        public static Dictionary<string, object> FindFrameNode(Dictionary<string, object> frameTreeNode, string frameId)
        {
            if (frameTreeNode == null || string.IsNullOrEmpty(frameId))
            {
                return null;
            }

            var frame = CdpValueConverter.GetDictionary(frameTreeNode, "frame");
            if (frame != null && CdpValueConverter.GetString(frame, "id") == frameId)
            {
                return frameTreeNode;
            }

            var childFrames = CdpValueConverter.GetList(frameTreeNode, "childFrames");
            if (childFrames == null)
            {
                return null;
            }

            foreach (var childEntry in childFrames)
            {
                var found = FindFrameNode(childEntry as Dictionary<string, object>, frameId);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public static Dictionary<string, object> MatchFrame(
            CdpClient client,
            IList<Dictionary<string, object>> frames,
            SelectorLevel level)
        {
            var matched = new List<Dictionary<string, object>>();
            foreach (var frame in frames)
            {
                if (FrameMatchesLevel(client, frame, level))
                {
                    matched.Add(frame);
                }
            }

            if (matched.Count == 0)
            {
                return null;
            }

            var idxProperty = level.Properties.FirstOrDefault(item =>
                string.Equals(item.Name, "idx", StringComparison.OrdinalIgnoreCase) &&
                item.IsSelected);

            var index = 0;
            if (idxProperty != null && !string.IsNullOrEmpty(idxProperty.Value))
            {
                int.TryParse(idxProperty.Value, out index);
            }

            if (index < 0 || index >= matched.Count)
            {
                return null;
            }

            return matched[index];
        }

        private static bool FrameMatchesLevel(CdpClient client, Dictionary<string, object> frame, SelectorLevel level)
        {
            var frameId = CdpValueConverter.GetString(frame, "id");
            var iframeAttributes = TryGetIframeElementAttributes(client, frameId);

            foreach (var property in level.Properties.Where(item => item.IsSelected))
            {
                if (string.Equals(property.Name, "idx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var actual = GetFramePropertyValue(frame, iframeAttributes, property.Name);
                if (!MatchValue(actual, property.Value, property.IsRegex))
                {
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<string, string> TryGetIframeElementAttributes(CdpClient client, string frameId)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (client == null || string.IsNullOrEmpty(frameId))
            {
                return attributes;
            }

            try
            {
                var owner = client.Send("DOM.getFrameOwner", new Dictionary<string, object>
                {
                    { "frameId", frameId }
                });

                var backendNodeId = CdpValueConverter.GetInt(owner, "backendNodeId");
                if (backendNodeId <= 0)
                {
                    return attributes;
                }

                var describe = client.Send("DOM.describeNode", new Dictionary<string, object>
                {
                    { "backendNodeId", backendNodeId }
                });

                var node = CdpValueConverter.GetDictionary(describe, "node");
                ParseDomAttributes(CdpValueConverter.GetList(node, "attributes"), attributes);
            }
            catch
            {
                // Ignore and fall back to frame-tree fields only.
            }

            return attributes;
        }

        private static void ParseDomAttributes(IList rawAttributes, IDictionary<string, string> attributes)
        {
            if (rawAttributes == null || attributes == null)
            {
                return;
            }

            for (var i = 0; i + 1 < rawAttributes.Count; i += 2)
            {
                var name = Convert.ToString(rawAttributes[i]);
                var value = Convert.ToString(rawAttributes[i + 1]);
                if (!string.IsNullOrEmpty(name))
                {
                    attributes[name] = value ?? string.Empty;
                }
            }
        }

        private static string GetFramePropertyValue(
            Dictionary<string, object> frame,
            IDictionary<string, string> iframeAttributes,
            string propertyName)
        {
            if (iframeAttributes != null && !string.IsNullOrEmpty(propertyName))
            {
                foreach (var pair in iframeAttributes)
                {
                    if (string.Equals(pair.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Value ?? string.Empty;
                    }
                }
            }

            if (string.Equals(propertyName, "src", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propertyName, "url", StringComparison.OrdinalIgnoreCase))
            {
                return CdpValueConverter.GetString(frame, "url") ?? string.Empty;
            }

            if (string.Equals(propertyName, "name", StringComparison.OrdinalIgnoreCase))
            {
                return CdpValueConverter.GetString(frame, "name") ?? string.Empty;
            }

            return CdpValueConverter.GetString(frame, propertyName) ?? string.Empty;
        }

        private static bool MatchValue(string actual, string expected, bool isRegex)
        {
            actual = actual ?? string.Empty;
            expected = expected ?? string.Empty;

            if (isRegex)
            {
                try
                {
                    return System.Text.RegularExpressions.Regex.IsMatch(actual, expected);
                }
                catch
                {
                    return false;
                }
            }

            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
