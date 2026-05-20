using System.Collections.Generic;
using F2B.Browser.IExplore;

namespace F2B.Browser.IExplore.Com
{
    internal sealed class ElementLocatorOptions
    {
        public Dictionary<string, string> Filters { get; private set; }
        public int Idx { get; private set; }
        public string Value { get; private set; }
        public MouseButton Button { get; private set; }
        public ClickMode Mode { get; private set; }
        public int ClickIntervalMs { get; private set; }

        public static ElementLocatorOptions Parse(IDictionary<string, object> locator, bool forInput)
        {
            var op = forInput ? LocatorOperation.Input : LocatorOperation.Click;
            var parsed = ElementLocatorParse.Parse(locator, op);
            return new ElementLocatorOptions
            {
                Filters = parsed.Filters,
                Idx = parsed.ElementIdx,
                Value = parsed.InputValue,
                Button = parsed.Button,
                Mode = parsed.Mode,
                ClickIntervalMs = parsed.ClickIntervalMs
            };
        }
    }
}
