using System.Collections.ObjectModel;
using F2B.Browser.IExplore.COM;
using IE.Inspector.Helpers;

namespace IE.Inspector.Models
{
    public class ElementPropertyItem : NotifyObject
    {
        private bool _isSelected;
        private bool _isRegex;
        private string _value;

        public string Name { get; set; }
        public bool CanToggle { get; set; } = true;

        public string Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                    RaisePropertyChanged(nameof(DisplayText));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public bool IsRegex
        {
            get => _isRegex;
            set
            {
                if (!SupportsRegexProperty(Name) && value)
                    return;

                if (SetProperty(ref _isRegex, value))
                    RaisePropertyChanged(nameof(DisplayText));
            }
        }

        public bool SupportsRegex => SupportsRegexProperty(Name);

        public static bool SupportsRegexProperty(string propertyName)
        {
            switch (propertyName)
            {
                case "name":
                case "id":
                case "tag":
                case "type":
                case "class":
                case "text":
                case "text_contains":
                case "text_re":
                case "value":
                case "title":
                case "selector":
                    return true;
                default:
                    return false;
            }
        }

        public string DisplayText
        {
            get
            {
                if (string.IsNullOrEmpty(Value))
                    return Name;

                var suffix = IsRegex ? "-re" : string.Empty;
                return Name + suffix + " = \"" + Value + "\"";
            }
        }
    }

    public class DomTreeNode : NotifyObject
    {
        private bool _isExpanded;
        private bool _isSelected;
        private bool _childrenLoaded;

        public DomTreeNode(IEWindowController.IEDomElement element, DomTreeNode parent)
        {
            Element = element;
            Parent = parent;
            Children = new ObservableCollection<DomTreeNode>();
            DisplayName = BuildDisplayName(element);
        }

        public IEWindowController.IEDomElement Element { get; }
        public DomTreeNode Parent { get; }
        public ObservableCollection<DomTreeNode> Children { get; }

        public string DisplayName { get; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (!SetProperty(ref _isExpanded, value))
                    return;

                if (value && !_childrenLoaded)
                    LoadChildren();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public void LoadChildren()
        {
            if (_childrenLoaded || Element == null)
                return;

            _childrenLoaded = true;
            foreach (var child in Element.get_children())
            {
                if (child == null)
                    continue;

                Children.Add(new DomTreeNode(child, this));
            }
        }

        private static string BuildDisplayName(IEWindowController.IEDomElement element)
        {
            if (element == null)
                return "Unknown";

            var tag = SafeTagName(element) ?? "element";
            var id = SafeAttribute(element, "id");
            var name = SafeAttribute(element, "name");
            var text = element.get_text();
            if (text != null && text.Length > 40)
                text = text.Substring(0, 40) + "...";

            var suffix = !string.IsNullOrEmpty(id) ? id
                : !string.IsNullOrEmpty(name) ? name
                : text ?? string.Empty;

            return (tag + " \"" + suffix + "\"").Trim();
        }

        private static string SafeTagName(IEWindowController.IEDomElement element)
        {
            try
            {
                var tag = element.get_tag_name();
                if (!string.IsNullOrWhiteSpace(tag))
                    return tag;
            }
            catch
            {
            }

            return SafeAttribute(element, "tagName");
        }

        private static string SafeAttribute(IEWindowController.IEDomElement element, string name)
        {
            try
            {
                return element.get_attribute(name)?.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
