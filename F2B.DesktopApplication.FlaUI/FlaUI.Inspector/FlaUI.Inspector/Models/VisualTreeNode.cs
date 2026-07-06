using System.Collections.ObjectModel;
using FlaUI.Core.AutomationElements;
using FlaUI.Inspector.Helpers;

namespace FlaUI.Inspector.Models
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
                case "Name":
                case "Title":
                case "AutomationId":
                case "ClassName":
                case "ProcessName":
                case "FrameworkId":
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

    public class VisualTreeNode : NotifyObject
    {
        private bool _isExpanded;
        private bool _isSelected;
        private bool _childrenLoaded;

        public VisualTreeNode(AutomationElement element, VisualTreeNode parent)
        {
            Element = element;
            Parent = parent;
            Children = new ObservableCollection<VisualTreeNode>();
            DisplayName = BuildDisplayName(element);
        }

        public AutomationElement Element { get; }
        public VisualTreeNode Parent { get; }
        public ObservableCollection<VisualTreeNode> Children { get; }

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
            foreach (var child in Element.FindAllChildren())
            {
                Children.Add(new VisualTreeNode(child, this));
            }
        }

        private static string BuildDisplayName(AutomationElement element)
        {
            var controlType = element.Properties.ControlType.IsSupported
                ? element.Properties.ControlType.Value.ToString()
                : "Unknown";
            var name = element.Properties.Name.IsSupported ? element.Properties.Name.Value : string.Empty;
            var automationId = element.Properties.AutomationId.IsSupported ? element.Properties.AutomationId.Value : string.Empty;
            return (controlType + " \"" + name + "\" " + automationId).Trim();
        }
    }
}
