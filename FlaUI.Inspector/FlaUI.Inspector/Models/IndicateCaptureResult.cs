using System.Collections.Generic;
using FlaUI.Core.AutomationElements;

namespace FlaUI.Inspector.Models
{
    /// <summary>
    /// Indicate 点击瞬间同步抓取的元素数据，避免菜单失焦后 UIA 树失效。
    /// </summary>
    public sealed class IndicateCaptureResult
    {
        public AutomationElement TargetElement { get; set; }
        public IList<SelectorLevel> SelectorLevels { get; set; }
        public IList<ElementPropertyItem> PropertyItems { get; set; }
        public VisualTreeNode VisualTreeRoot { get; set; }
        public VisualTreeNode SelectedVisualNode { get; set; }
        public string TargetDisplay { get; set; }
        public string SelectorXmlSnapshot { get; set; }
        public int PathDepth { get; set; }
    }
}
