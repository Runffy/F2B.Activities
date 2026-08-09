using System.Collections.Generic;
using F2B.Forms.Model;

namespace F2B.Forms.Designer
{
    internal sealed class DesignClipboardItem
    {
        public ControlDefinition Definition { get; set; }
        public string ParentId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    internal sealed class DesignClipboard
    {
        private readonly List<DesignClipboardItem> _items = new List<DesignClipboardItem>();

        public bool HasContent
        {
            get { return _items.Count > 0; }
        }

        public int PasteCount { get; set; }

        public IList<DesignClipboardItem> Items
        {
            get { return _items; }
        }

        public void Set(IEnumerable<DesignClipboardItem> items)
        {
            _items.Clear();
            PasteCount = 0;
            if (items == null)
            {
                return;
            }

            foreach (DesignClipboardItem item in items)
            {
                if (item != null && item.Definition != null)
                {
                    _items.Add(item);
                }
            }
        }

        public void Clear()
        {
            _items.Clear();
            PasteCount = 0;
        }
    }
}
