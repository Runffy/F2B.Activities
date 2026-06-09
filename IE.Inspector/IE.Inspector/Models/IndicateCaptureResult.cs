using System.Collections.Generic;
using F2B.Browser.IExplore.COM;

namespace IE.Inspector.Models
{
    public sealed class IndicateCaptureResult
    {
        public IEWindowController Controller { get; set; }
        public IEWindowController.IEDomElement TargetElement { get; set; }
        public IList<object> FramePath { get; set; }
        public IList<SelectorLevel> SelectorLevels { get; set; }
        public IList<ElementPropertyItem> PropertyItems { get; set; }
        public DomTreeNode DomTreeRoot { get; set; }
        public DomTreeNode SelectedDomNode { get; set; }
        public string TargetDisplay { get; set; }
        public string SelectorXmlSnapshot { get; set; }
        public int PathDepth { get; set; }
    }
}
