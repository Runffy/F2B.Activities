using System.Collections.Generic;

namespace F2B.Browser.Chromium.Bridge.Selectors
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

        /// <summary>First &lt;wnd&gt; level, if present.</summary>
        public SelectorLevel TabLevel { get; }

        /// <summary>Zero or more &lt;frm&gt; levels (iframe chain).</summary>
        public IList<SelectorLevel> FrameLevels { get; }

        /// <summary>&lt;ctrl&gt; levels after wnd/frm.</summary>
        public IList<SelectorLevel> ElementLevels { get; }

        public bool RequiresTabResolution
        {
            get { return TabLevel != null; }
        }
    }
}
