using System.Collections.Generic;

namespace F2B.Browser.Chromium.Cdp.Selectors
{
    public sealed class SelectorScope
    {
        public SelectorScope(
            SelectorLevel tabLevel,
            IList<SelectorLevel> frameLevels,
            IList<SelectorLevel> elementLevels)
        {
            TabLevel = tabLevel;
            FrameLevels = frameLevels ?? new List<SelectorLevel>();
            ElementLevels = elementLevels ?? new List<SelectorLevel>();
        }

        public SelectorLevel TabLevel { get; }

        public IList<SelectorLevel> FrameLevels { get; }

        public IList<SelectorLevel> ElementLevels { get; }

        public bool RequiresTabResolution
        {
            get { return TabLevel != null; }
        }
    }
}
