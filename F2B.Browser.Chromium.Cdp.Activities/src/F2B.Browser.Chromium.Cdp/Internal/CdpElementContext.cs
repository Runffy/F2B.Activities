using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal sealed class CdpElementContext
    {
        private readonly CdpElement _element;
        private CdpElementRect _rect;
        private CdpElementStates _states;
        private CdpElementPseudo _pseudo;

        internal CdpElementContext(CdpElement element)
        {
            _element = element;
        }

        internal CdpTabSession Session
        {
            get { return _element.Tab.GetSession(); }
        }

        internal CdpElementRect Rect
        {
            get { return _rect ?? (_rect = new CdpElementRect(_element, this)); }
        }

        internal CdpElementStates States
        {
            get { return _states ?? (_states = new CdpElementStates(_element, this)); }
        }

        internal CdpElementPseudo Pseudo
        {
            get { return _pseudo ?? (_pseudo = new CdpElementPseudo(_element, this)); }
        }

        internal string GetHtml()
        {
            RefreshIdsIfNeeded();
            var result = Send("DOM.getOuterHTML", new Dictionary<string, object>
            {
                { "backendNodeId", _element.BackendNodeId }
            });
            return CdpValueConverter.GetString(result, "outerHTML") ?? string.Empty;
        }

        internal string GetInnerHtml()
        {
            return RunJsString(CdpElementScripts.InnerHtml);
        }

        internal string GetRawText()
        {
            return RunJsString(CdpElementScripts.RawText);
        }

        internal string GetInnerText()
        {
            return RunJsString(CdpElementScripts.InnerText);
        }

        internal string GetText()
        {
            return RunJsString(CdpElementScripts.FormattedText);
        }

        internal Dictionary<string, string> GetAttrs()
        {
            RefreshIdsIfNeeded();
            try
            {
                return ReadAttributes(_element.NodeId);
            }
            catch (BrowserException)
            {
                RefreshIds();
                return ReadAttributes(_element.NodeId);
            }
        }

        internal Dictionary<string, string> GetProperties()
        {
            const string script =
                @"var result = {};
var names = ['id','name','value','type','checked','disabled','selected','href','src','className','tagName','innerText','innerHTML','textContent','placeholder','title','defaultValue','readOnly','required','multiple','selectedIndex','nodeName'];
for (var i = 0; i < names.length; i++) {
  var key = names[i];
  try {
    var v = this[key];
    if (v === undefined || v === null) continue;
    if (typeof v === 'function') continue;
    if (typeof v === 'object') {
      try { result[key] = JSON.stringify(v); } catch (e) { result[key] = String(v); }
    } else {
      result[key] = String(v);
    }
  } catch (e) {}
}
return result;";

            var raw = RunJs(script);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var map = raw as Dictionary<string, object>;
            if (map == null)
            {
                var generic = raw as System.Collections.IDictionary;
                if (generic != null)
                {
                    foreach (System.Collections.DictionaryEntry entry in generic)
                    {
                        if (entry.Key != null)
                        {
                            dict[Convert.ToString(entry.Key)] = entry.Value == null ? string.Empty : Convert.ToString(entry.Value);
                        }
                    }
                }

                return dict;
            }

            foreach (var pair in map)
            {
                dict[pair.Key] = pair.Value == null ? string.Empty : Convert.ToString(pair.Value);
            }

            return dict;
        }

        internal string GetAttr(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            var attrs = GetAttrs();
            if (string.Equals(name, "text", StringComparison.OrdinalIgnoreCase))
            {
                return GetText();
            }

            if (string.Equals(name, "innerText", StringComparison.OrdinalIgnoreCase))
            {
                return GetRawText();
            }

            if (string.Equals(name, "html", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "outerHTML", StringComparison.OrdinalIgnoreCase))
            {
                return GetHtml();
            }

            if (string.Equals(name, "innerHTML", StringComparison.OrdinalIgnoreCase))
            {
                return GetInnerHtml();
            }

            string value;
            if (!attrs.TryGetValue(name, out value))
            {
                return null;
            }

            if (string.Equals(name, "href", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "src", StringComparison.OrdinalIgnoreCase))
            {
                return CdpLinkHelper.MakeAbsolute(value, GetBaseUri());
            }

            return value;
        }

        internal string GetLink()
        {
            var href = GetAttr("href");
            if (!string.IsNullOrEmpty(href))
            {
                return href;
            }

            return GetAttr("src");
        }

        internal string GetValue()
        {
            var value = RunJs(CdpElementScripts.Value);
            return value == null ? null : Convert.ToString(value);
        }

        internal string GetProperty(string name)
        {
            var propertyName = CdpElementPropertyHelper.NormalizePropertyName(name);
            if (string.Equals(propertyName, "value", StringComparison.Ordinal))
            {
                return GetValue();
            }

            if (string.Equals(propertyName, "innerHTML", StringComparison.Ordinal))
            {
                return GetInnerHtml();
            }

            if (string.Equals(propertyName, "outerHTML", StringComparison.Ordinal))
            {
                return GetHtml();
            }

            if (string.Equals(propertyName, "innerText", StringComparison.Ordinal))
            {
                return GetRawText();
            }

            return RunJsString(string.Format(CdpElementScripts.PropertyTemplate, propertyName));
        }

        internal string GetStyle(string style, string pseudoElement)
        {
            var pseudo = string.IsNullOrEmpty(pseudoElement)
                ? string.Empty
                : string.Format(", \"{0}\"", pseudoElement.Trim(':', ' '));
            return RunJsString(string.Format(CdpElementScripts.StyleTemplate, pseudo, EscapeJsString(style)));
        }

        internal string GetXpath()
        {
            return RunJsString(CdpElementScripts.ElementPathXpath);
        }

        internal string GetCssSelector()
        {
            return RunJsString(CdpElementScripts.ElementPathCss);
        }

        internal int GetChildCount()
        {
            return GetChildren().Length;
        }

        internal CdpElement[] GetChildren()
        {
            RefreshIdsIfNeeded();
            var describe = Send("DOM.describeNode", new Dictionary<string, object>
            {
                { "nodeId", _element.NodeId },
                { "depth", 1 },
                { "pierce", true }
            });

            var node = CdpValueConverter.GetDictionary(describe, "node");
            var children = node != null ? CdpValueConverter.GetList(node, "children") : null;
            if (children == null || children.Count == 0)
            {
                return new CdpElement[0];
            }

            var result = new List<CdpElement>();
            foreach (var childEntry in children)
            {
                var childNode = childEntry as Dictionary<string, object>;
                if (childNode == null)
                {
                    continue;
                }

                var backendNodeId = CdpValueConverter.GetInt(childNode, "backendNodeId");
                var nodeId = CdpValueConverter.GetInt(childNode, "nodeId");
                if (backendNodeId <= 0)
                {
                    continue;
                }

                var tag = CdpValueConverter.GetString(childNode, "localName") ?? string.Empty;
                var objectId = ResolveObjectId(backendNodeId, nodeId);
                result.Add(new CdpElement(_element.Tab, tag, backendNodeId, nodeId, objectId));
            }

            return result.ToArray();
        }

        internal object RunJs(
            string script,
            object[] args = null,
            bool asExpression = false,
            bool isAsync = false,
            int timeoutMs = 30000)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                throw new ArgumentNullException("script");
            }

            if (isAsync)
            {
                var scriptCopy = script;
                var argsCopy = args;
                ThreadPool.QueueUserWorkItem(_ => RunJsCore(scriptCopy, argsCopy, asExpression, timeoutMs));
                return null;
            }

            return RunJsCore(script, args, asExpression, timeoutMs);
        }

        internal string RunJsString(string script, int timeoutMs = 30000)
        {
            var result = RunJs(script, null, false, false, timeoutMs);
            return result == null ? string.Empty : Convert.ToString(result);
        }

        internal void ScrollToSee(bool center)
        {
            RunJs(string.Format(CdpElementScripts.ScrollIntoView, center ? "true" : "false"));
            if (center)
            {
                RunJs(CdpElementScripts.ScrollToCenter);
            }
        }

        internal void PrepareForAction()
        {
            ScrollToSee(true);
        }

        internal Dictionary<string, object> Send(string method, Dictionary<string, object> parameters = null)
        {
            return Session.Send(method, parameters);
        }

        internal Dictionary<string, object> Send(
            string method,
            Dictionary<string, object> parameters,
            TimeSpan timeout)
        {
            return Session.Send(method, parameters, timeout);
        }

        internal IList<double> GetBoxQuad(string quadName)
        {
            RefreshIdsIfNeeded();
            var parameters = new Dictionary<string, object>();
            if (_element.BackendNodeId > 0)
            {
                parameters["backendNodeId"] = _element.BackendNodeId;
            }
            else if (_element.NodeId > 0)
            {
                parameters["nodeId"] = _element.NodeId;
            }
            else if (!string.IsNullOrEmpty(_element.ObjectId))
            {
                parameters["objectId"] = _element.ObjectId;
            }

            var response = Send("DOM.getBoxModel", parameters);
            var model = CdpValueConverter.GetDictionary(response, "model");
            var quad = model != null ? CdpValueConverter.GetList(model, quadName) : null;
            if (quad == null || quad.Count < 8)
            {
                throw new BrowserException("Element has no box model.");
            }

            var values = new List<double>(8);
            foreach (var item in quad)
            {
                values.Add(Convert.ToDouble(item));
            }

            return values;
        }

        internal bool TryGetBoxQuad(string quadName, out IList<double> quad)
        {
            try
            {
                quad = GetBoxQuad(quadName);
                return true;
            }
            catch (BrowserException)
            {
                quad = null;
                return false;
            }
        }

        internal CdpTabRectSnapshot GetTabRect()
        {
            return Session.GetRect();
        }

        internal bool IsLocationInViewport(int pageX, int pageY)
        {
            var result = RunJs(string.Format(CdpElementScripts.LocationInViewportTemplate, pageX, pageY));
            return result is bool && (bool)result;
        }

        internal void RefreshIds()
        {
            if (!string.IsNullOrEmpty(_element.ObjectId))
            {
                var request = Send("DOM.requestNode", new Dictionary<string, object>
                {
                    { "objectId", _element.ObjectId }
                });
                _element.NodeId = CdpValueConverter.GetInt(request, "nodeId");
            }
            else if (_element.BackendNodeId > 0)
            {
                var resolved = Send("DOM.resolveNode", new Dictionary<string, object>
                {
                    { "backendNodeId", _element.BackendNodeId }
                });
                var obj = CdpValueConverter.GetDictionary(resolved, "object");
                _element.ObjectId = CdpValueConverter.GetString(obj, "objectId") ?? string.Empty;
                var request = Send("DOM.requestNode", new Dictionary<string, object>
                {
                    { "objectId", _element.ObjectId }
                });
                _element.NodeId = CdpValueConverter.GetInt(request, "nodeId");
            }

            var describe = Send("DOM.describeNode", new Dictionary<string, object>
            {
                { "backendNodeId", _element.BackendNodeId }
            });
            var node = CdpValueConverter.GetDictionary(describe, "node");
            if (node != null)
            {
                _element.Tag = CdpValueConverter.GetString(node, "localName") ?? _element.Tag;
                _element.BackendNodeId = CdpValueConverter.GetInt(node, "backendNodeId", _element.BackendNodeId);
                _element.NodeId = CdpValueConverter.GetInt(node, "nodeId", _element.NodeId);
            }
        }

        private object RunJsCore(string script, object[] args, bool asExpression, int timeoutMs)
        {
            EnsureCanRunJs();
            RefreshIdsIfNeeded();
            var timeout = TimeSpan.FromMilliseconds(timeoutMs <= 0 ? 30000 : timeoutMs);

            Dictionary<string, object> response;
            if (asExpression)
            {
                response = Send("Runtime.callFunctionOn", new Dictionary<string, object>
                {
                    { "functionDeclaration", "function() { return (" + script + "); }" },
                    { "objectId", _element.ObjectId },
                    { "arguments", BuildCallFunctionArguments(null) },
                    { "returnByValue", true },
                    { "awaitPromise", true },
                    { "userGesture", true }
                }, timeout);
            }
            else
            {
                response = Send("Runtime.callFunctionOn", new Dictionary<string, object>
                {
                    { "functionDeclaration", CdpJsScript.WrapAsFunction(script) },
                    { "objectId", _element.ObjectId },
                    { "arguments", BuildCallFunctionArguments(args) },
                    { "returnByValue", true },
                    { "awaitPromise", true },
                    { "userGesture", true }
                }, timeout);
            }

            return ParseRuntimeResult(response);
        }

        private void EnsureCanRunJs()
        {
            if (Session.HasAlert)
            {
                throw new BrowserException("JavaScript dialog is open.");
            }

            if (string.IsNullOrEmpty(_element.ObjectId))
            {
                RefreshIds();
            }

            if (string.IsNullOrEmpty(_element.ObjectId))
            {
                throw new BrowserException("Element object id is unavailable.");
            }
        }

        private void RefreshIdsIfNeeded()
        {
            if (_element.NodeId <= 0 || string.IsNullOrEmpty(_element.ObjectId))
            {
                RefreshIds();
            }
        }

        private string GetBaseUri()
        {
            var value = RunJs(CdpElementScripts.BaseUri);
            return value == null ? _element.Tab.Url : Convert.ToString(value);
        }

        private Dictionary<string, string> ReadAttributes(int nodeId)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (nodeId <= 0)
            {
                return result;
            }

            var response = Send("DOM.getAttributes", new Dictionary<string, object>
            {
                { "nodeId", nodeId }
            });
            var attrs = CdpValueConverter.GetList(response, "attributes");
            if (attrs == null)
            {
                return result;
            }

            for (var i = 0; i + 1 < attrs.Count; i += 2)
            {
                var name = Convert.ToString(attrs[i]);
                var value = Convert.ToString(attrs[i + 1]);
                if (!string.IsNullOrEmpty(name))
                {
                    result[name] = value ?? string.Empty;
                }
            }

            return result;
        }

        private string ResolveObjectId(int backendNodeId, int nodeId)
        {
            Dictionary<string, object> resolved;
            if (nodeId > 0)
            {
                resolved = Send("DOM.resolveNode", new Dictionary<string, object>
                {
                    { "nodeId", nodeId }
                });
            }
            else
            {
                resolved = Send("DOM.resolveNode", new Dictionary<string, object>
                {
                    { "backendNodeId", backendNodeId }
                });
            }

            var obj = CdpValueConverter.GetDictionary(resolved, "object");
            return CdpValueConverter.GetString(obj, "objectId") ?? string.Empty;
        }

        private static IList BuildCallFunctionArguments(object[] args)
        {
            var list = new List<Dictionary<string, object>>();
            if (args == null)
            {
                return list;
            }

            foreach (var arg in args)
            {
                list.Add(new Dictionary<string, object> { { "value", arg } });
            }

            return list;
        }

        private static object ParseRuntimeResult(Dictionary<string, object> response)
        {
            if (response == null)
            {
                return null;
            }

            object exceptionDetails;
            if (response.TryGetValue("exceptionDetails", out exceptionDetails) && exceptionDetails != null)
            {
                throw new BrowserException(string.Format("JavaScript execution failed: {0}", exceptionDetails));
            }

            var inner = CdpValueConverter.GetDictionary(response, "result");
            if (inner == null)
            {
                return null;
            }

            object value;
            if (inner.TryGetValue("value", out value))
            {
                return value;
            }

            object unserializableValue;
            if (inner.TryGetValue("unserializableValue", out unserializableValue))
            {
                return unserializableValue;
            }

            if (string.Equals(CdpValueConverter.GetString(inner, "subtype"), "null", StringComparison.Ordinal))
            {
                return null;
            }

            return null;
        }

        private static string EscapeJsString(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
