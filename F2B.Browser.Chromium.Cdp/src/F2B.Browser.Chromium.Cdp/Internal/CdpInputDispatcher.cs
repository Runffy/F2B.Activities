using System;
using System.Collections.Generic;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal sealed class CdpInputDispatcher
    {
        private readonly CdpTabSession _session;
        private int _currX;
        private int _currY;

        internal CdpInputDispatcher(CdpTabSession session)
        {
            _session = session;
        }

        internal void Click(int viewportX, int viewportY, CdpMouseButton button, int count)
        {
            var buttonName = ToButtonName(button);
            var parameters = new Dictionary<string, object>
            {
                { "type", "mousePressed" },
                { "x", viewportX },
                { "y", viewportY },
                { "button", buttonName },
                { "clickCount", count },
                { "modifiers", 0 }
            };
            _session.Send("Input.dispatchMouseEvent", parameters);
            parameters["type"] = "mouseReleased";
            parameters["clickCount"] = 1;
            _session.Send("Input.dispatchMouseEvent", parameters);
            _currX = viewportX;
            _currY = viewportY;
        }

        internal void MoveTo(int viewportX, int viewportY, double durationSeconds)
        {
            var duration = durationSeconds <= 0 ? 0.02 : durationSeconds;
            var steps = Math.Max(1, (int)Math.Round(duration * 50));
            for (var i = 1; i <= steps; i++)
            {
                var x = _currX + (int)Math.Round((viewportX - _currX) * (i / (double)steps));
                var y = _currY + (int)Math.Round((viewportY - _currY) * (i / (double)steps));
                _session.Send("Input.dispatchMouseEvent", new Dictionary<string, object>
                {
                    { "type", "mouseMoved" },
                    { "x", x },
                    { "y", y },
                    { "button", "left" },
                    { "modifiers", 0 }
                });
                _currX = x;
                _currY = y;
                if (duration > 0)
                {
                    Thread.Sleep(20);
                }
            }
        }

        internal void Drag(
            int startX,
            int startY,
            int endX,
            int endY,
            double durationSeconds)
        {
            MoveTo(startX, startY, 0.1);
            _session.Send("Input.dispatchMouseEvent", new Dictionary<string, object>
            {
                { "type", "mousePressed" },
                { "x", startX },
                { "y", startY },
                { "button", "left" },
                { "clickCount", 1 },
                { "modifiers", 0 }
            });
            MoveTo(endX, endY, durationSeconds);
            _session.Send("Input.dispatchMouseEvent", new Dictionary<string, object>
            {
                { "type", "mouseReleased" },
                { "x", endX },
                { "y", endY },
                { "button", "left" },
                { "clickCount", 1 },
                { "modifiers", 0 }
            });
            _currX = endX;
            _currY = endY;
        }

        internal void Hover(int viewportX, int viewportY)
        {
            MoveTo(viewportX, viewportY, 0.1);
        }

        internal void InsertText(string text)
        {
            CdpKeyInput.InputTextOrKeys(_session, text);
        }

        internal void SendKeys(params CdpKey[] keys)
        {
            CdpKeyInput.Type(_session, keys);
        }

        private static string ToButtonName(CdpMouseButton button)
        {
            switch (button)
            {
                case CdpMouseButton.Middle:
                    return "middle";
                case CdpMouseButton.Right:
                    return "right";
                default:
                    return "left";
            }
        }
    }
}
