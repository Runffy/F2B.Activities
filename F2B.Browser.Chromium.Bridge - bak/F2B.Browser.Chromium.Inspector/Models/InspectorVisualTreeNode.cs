using System.Collections.ObjectModel;
using F2B.Browser.Chromium.Bridge;
using F2B.Browser.Chromium.Inspector.Helpers;

namespace F2B.Browser.Chromium.Inspector.Models
{
    public sealed class InspectorVisualTreeNode : NotifyObject
    {
        private bool _isExpanded;
        private bool _isSelected;
        private bool _childrenLoaded;

        public InspectorVisualTreeNode(string displayName, object[] segments, object[] loadSegments, InspectorVisualTreeNode parent)
        {
            DisplayName = displayName ?? string.Empty;
            Segments = segments ?? new object[0];
            LoadSegments = loadSegments ?? segments ?? new object[0];
            Parent = parent;
            Children = new ObservableCollection<InspectorVisualTreeNode>();
        }

        public string DisplayName { get; }

        public object[] Segments { get; }

        public object[] LoadSegments { get; }

        public InspectorVisualTreeNode Parent { get; }

        public ObservableCollection<InspectorVisualTreeNode> Children { get; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (!SetProperty(ref _isExpanded, value))
                    return;

                if (value && !_childrenLoaded)
                    RequestLoadChildren?.Invoke(this);
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public bool ChildrenLoaded
        {
            get => _childrenLoaded;
            set => SetProperty(ref _childrenLoaded, value);
        }

        public System.Action<InspectorVisualTreeNode> RequestLoadChildren { get; set; }

        public static InspectorVisualTreeNode FromDomNode(BridgeInspectorDomNode node, InspectorVisualTreeNode parent)
        {
            var loadSegments = node.FrameSegments ?? node.Segments ?? new object[0];
            return new InspectorVisualTreeNode(node.DisplayName, node.Segments ?? loadSegments, loadSegments, parent);
        }
    }
}
