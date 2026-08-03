using System;
using System.Collections.Generic;

namespace F2B.Browser.Chromium.Cdp.Browser
{
    public sealed class CdpElementRect
    {
        private readonly CdpElement _element;
        private readonly Internal.CdpElementContext _context;

        internal CdpElementRect(CdpElement element, Internal.CdpElementContext context)
        {
            _element = element;
            _context = context;
        }

        public Tuple<int, int> Size
        {
            get
            {
                var border = _context.GetBoxQuad("border");
                var width = (int)Math.Round(border[2] - border[0]);
                var height = (int)Math.Round(border[5] - border[1]);
                return Tuple.Create(width, height);
            }
        }

        public Tuple<int, int> Location
        {
            get { return ToPageCoord(ViewportLocation); }
        }

        public Tuple<int, int> Midpoint
        {
            get { return ToPageCoord(ViewportMidpoint); }
        }

        public Tuple<int, int> ClickPoint
        {
            get { return ToPageCoord(ViewportClickPoint); }
        }

        public IList<Tuple<int, int>> Corners
        {
            get { return ToPageCorners(_context.GetBoxQuad("border")); }
        }

        public IList<Tuple<int, int>> ViewportCorners
        {
            get { return ToViewportCorners(_context.GetBoxQuad("border")); }
        }

        public Tuple<int, int> ViewportLocation
        {
            get
            {
                var border = _context.GetBoxQuad("border");
                return Tuple.Create((int)Math.Round(border[0]), (int)Math.Round(border[1]));
            }
        }

        public Tuple<int, int> ViewportMidpoint
        {
            get
            {
                var border = _context.GetBoxQuad("border");
                var x = (int)Math.Round((border[0] + border[2]) / 2.0);
                var y = (int)Math.Round((border[1] + border[5]) / 2.0);
                return Tuple.Create(x, y);
            }
        }

        public Tuple<int, int> ViewportClickPoint
        {
            get
            {
                var padding = _context.GetBoxQuad("padding");
                var midpoint = ViewportMidpoint;
                return Tuple.Create(midpoint.Item1, (int)Math.Round(padding[1] + 3));
            }
        }

        public Tuple<int, int> ScreenLocation
        {
            get { return ToScreenCoord(ViewportLocation); }
        }

        public Tuple<int, int> ScreenMidpoint
        {
            get { return ToScreenCoord(ViewportMidpoint); }
        }

        public Tuple<int, int> ScreenClickPoint
        {
            get { return ToScreenCoord(ViewportClickPoint); }
        }

        public Tuple<int, int> ScrollPosition
        {
            get
            {
                var parts = _context.RunJsString(Internal.CdpElementScripts.ScrollPosition).Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);
                var x = parts.Length > 0 ? ParseInt(parts[0]) : 0;
                var y = parts.Length > 1 ? ParseInt(parts[1]) : 0;
                return Tuple.Create(x, y);
            }
        }

        private Tuple<int, int> ToPageCoord(Tuple<int, int> viewportPoint)
        {
            var scroll = GetViewportScroll();
            return Tuple.Create(viewportPoint.Item1 + scroll.Item1, viewportPoint.Item2 + scroll.Item2);
        }

        private Tuple<int, int> ToScreenCoord(Tuple<int, int> viewportPoint)
        {
            var tabRect = _context.GetTabRect();
            var pixelRatio = GetDevicePixelRatio();
            var vx = tabRect.ViewportLocation.Item1 + viewportPoint.Item1;
            var vy = tabRect.ViewportLocation.Item2 + viewportPoint.Item2;
            return Tuple.Create((int)Math.Round(vx * pixelRatio), (int)Math.Round(vy * pixelRatio));
        }

        private IList<Tuple<int, int>> ToPageCorners(IList<double> quad)
        {
            var scroll = GetViewportScroll();
            return new[]
            {
                Tuple.Create((int)Math.Round(quad[0] + scroll.Item1), (int)Math.Round(quad[1] + scroll.Item2)),
                Tuple.Create((int)Math.Round(quad[2] + scroll.Item1), (int)Math.Round(quad[3] + scroll.Item2)),
                Tuple.Create((int)Math.Round(quad[4] + scroll.Item1), (int)Math.Round(quad[5] + scroll.Item2)),
                Tuple.Create((int)Math.Round(quad[6] + scroll.Item1), (int)Math.Round(quad[7] + scroll.Item2))
            };
        }

        private static IList<Tuple<int, int>> ToViewportCorners(IList<double> quad)
        {
            return new[]
            {
                Tuple.Create((int)Math.Round(quad[0]), (int)Math.Round(quad[1])),
                Tuple.Create((int)Math.Round(quad[2]), (int)Math.Round(quad[3])),
                Tuple.Create((int)Math.Round(quad[4]), (int)Math.Round(quad[5])),
                Tuple.Create((int)Math.Round(quad[6]), (int)Math.Round(quad[7]))
            };
        }

        private Tuple<int, int> GetViewportScroll()
        {
            var layout = _context.Send("Page.getLayoutMetrics");
            var visual = Internal.CdpValueConverter.GetDictionary(layout, "visualViewport");
            var pageX = Internal.CdpValueConverter.GetInt(visual, "pageX");
            var pageY = Internal.CdpValueConverter.GetInt(visual, "pageY");
            return Tuple.Create(pageX, pageY);
        }

        private double GetDevicePixelRatio()
        {
            var value = _context.Session.Evaluate("window.devicePixelRatio || 1;");
            double ratio;
            return value != null && double.TryParse(Convert.ToString(value), out ratio) ? ratio : 1.0;
        }

        private static int ParseInt(string value)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : 0;
        }
    }
}
