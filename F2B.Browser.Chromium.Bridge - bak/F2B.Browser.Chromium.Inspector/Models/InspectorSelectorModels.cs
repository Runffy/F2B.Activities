using System.Collections.ObjectModel;
using F2B.Browser.Chromium.Bridge.Selectors;
using F2B.Browser.Chromium.Inspector.Helpers;
using F2B.Browser.Chromium.Inspector.Services;

namespace F2B.Browser.Chromium.Inspector.Models
{
    public sealed class InspectorPropertyItem : NotifyObject
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
                if (!SupportsRegex && value)
                    return;

                if (SetProperty(ref _isRegex, value))
                    RaisePropertyChanged(nameof(DisplayText));
            }
        }

        public bool SupportsRegex => SelectorProperty.SupportsRegexProperty(Name);

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

        public static InspectorPropertyItem FromSelectorProperty(SelectorProperty source)
        {
            return new InspectorPropertyItem
            {
                Name = source.Name,
                Value = source.Value,
                IsSelected = source.IsSelected && InspectorSelectorSerializer.IsCompactPropertyValue(source.Value),
                IsRegex = source.IsRegex,
                CanToggle = true
            };
        }

        public SelectorProperty ToSelectorProperty()
        {
            return new SelectorProperty
            {
                Name = Name,
                Value = Value,
                IsSelected = IsSelected,
                IsRegex = IsRegex
            };
        }
    }

    public sealed class InspectorSelectorLevel : NotifyObject
    {
        private bool _isEnabled = true;
        private bool _canDisable = true;
        private string _tagLine = string.Empty;

        public InspectorSelectorLevel(string tagName)
        {
            TagName = tagName ?? "ctrl";
            Properties = new ObservableCollection<InspectorPropertyItem>();
        }

        public string TagName { get; }

        public ObservableCollection<InspectorPropertyItem> Properties { get; }

        public string TagLine
        {
            get => _tagLine;
            private set => SetProperty(ref _tagLine, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (SetProperty(ref _isEnabled, value))
                    RefreshTagLine();
            }
        }

        public bool CanDisable
        {
            get => _canDisable;
            set => SetProperty(ref _canDisable, value);
        }

        public void RefreshTagLine()
        {
            TagLine = InspectorSelectorSerializer.SerializeLevelDisplayTag(this);
        }

        public SelectorLevel ToSelectorLevel()
        {
            var level = new SelectorLevel(TagName)
            {
                IsEnabled = IsEnabled,
                CanDisable = CanDisable
            };

            foreach (var property in Properties)
                level.Properties.Add(property.ToSelectorProperty());

            return level;
        }

        public static InspectorSelectorLevel FromSelectorLevel(SelectorLevel source)
        {
            var level = new InspectorSelectorLevel(source.TagName)
            {
                IsEnabled = source.IsEnabled,
                CanDisable = source.CanDisable
            };

            foreach (var property in source.Properties)
                level.Properties.Add(InspectorPropertyItem.FromSelectorProperty(property));

            level.RefreshTagLine();
            return level;
        }
    }
}
