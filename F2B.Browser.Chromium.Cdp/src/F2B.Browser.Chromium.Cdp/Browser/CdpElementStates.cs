using System;
using System.Collections.Generic;

namespace F2B.Browser.Chromium.Cdp.Browser
{
    public sealed class CdpElementStates
    {
        private readonly CdpElement _element;
        private readonly Internal.CdpElementContext _context;

        internal CdpElementStates(CdpElement element, Internal.CdpElementContext context)
        {
            _element = element;
            _context = context;
        }

        public bool IsSelected
        {
            get { return Convert.ToBoolean(_context.RunJs(Internal.CdpElementScripts.IsSelected)); }
        }

        public bool IsChecked
        {
            get { return Convert.ToBoolean(_context.RunJs(Internal.CdpElementScripts.IsChecked)); }
        }

        public bool IsDisplayed
        {
            get { return Convert.ToBoolean(_context.RunJs(Internal.CdpElementScripts.IsDisplayed)); }
        }

        public bool IsEnabled
        {
            get { return Convert.ToBoolean(_context.RunJs(Internal.CdpElementScripts.IsEnabled)); }
        }

        public bool IsAlive
        {
            get
            {
                try
                {
                    _context.RefreshIds();
                    var describe = _context.Send("DOM.describeNode", new Dictionary<string, object>
                    {
                        { "backendNodeId", _element.BackendNodeId }
                    });
                    var node = Internal.CdpValueConverter.GetDictionary(describe, "node");
                    return Internal.CdpValueConverter.GetInt(node, "nodeId") != 0;
                }
                catch (Exceptions.BrowserException)
                {
                    return false;
                }
            }
        }

        public bool IsInViewport
        {
            get
            {
                if (HasRect is bool)
                {
                    return false;
                }

                var clickPoint = _element.Rect.ClickPoint;
                return _context.IsLocationInViewport(clickPoint.Item1, clickPoint.Item2);
            }
        }

        public bool IsWholeInViewport
        {
            get
            {
                var location = _element.Rect.Location;
                var size = _element.Rect.Size;
                return _context.IsLocationInViewport(location.Item1, location.Item2) &&
                       _context.IsLocationInViewport(location.Item1 + size.Item1, location.Item2 + size.Item2);
            }
        }

        public object IsCovered
        {
            get
            {
                var clickPoint = _element.Rect.ClickPoint;
                try
                {
                    var response = _context.Send("DOM.getNodeForLocation", new Dictionary<string, object>
                    {
                        { "x", clickPoint.Item1 },
                        { "y", clickPoint.Item2 }
                    });
                    var backendNodeId = Internal.CdpValueConverter.GetInt(response, "backendNodeId");
                    return backendNodeId > 0 && backendNodeId != _element.BackendNodeId
                        ? (object)backendNodeId
                        : false;
                }
                catch (Exceptions.BrowserException)
                {
                    return false;
                }
            }
        }

        public bool IsClickable
        {
            get
            {
                return HasRect != null &&
                       IsEnabled &&
                       IsDisplayed &&
                       !string.Equals(_element.Style("pointer-events"), "none", StringComparison.OrdinalIgnoreCase);
            }
        }

        public object HasRect
        {
            get
            {
                IList<double> quad;
                if (!_context.TryGetBoxQuad("border", out quad))
                {
                    return false;
                }

                return _element.Rect.Corners;
            }
        }
    }
}
