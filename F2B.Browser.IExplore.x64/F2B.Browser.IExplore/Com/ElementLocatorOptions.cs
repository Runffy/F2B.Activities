using System.Collections.Generic;
using F2B.Browser.IExplore;

namespace F2B.Browser.IExplore.Com
{
    internal sealed class ElementLocatorOptions
    {
        public ParsedElementLocator Parsed { get; private set; }
        public Dictionary<string, string> Filters => Parsed.Filters;
        public int Idx => Parsed.ElementIdx;
        public string Value { get; private set; }
        public MouseButton Button => Parsed.Button;
        public ClickMode Mode => Parsed.Mode;
        public int ClickIntervalMs => Parsed.ClickIntervalMs;

        public static ElementLocatorOptions Parse(IDictionary<string, object> locator, bool forInput)
        {
            var op = forInput ? LocatorOperation.Input : LocatorOperation.Click;
            var parsed = ElementLocatorParse.Parse(locator, op);
            return new ElementLocatorOptions
            {
                Parsed = parsed,
                Value = parsed.InputValue
            };
        }
    }
}
