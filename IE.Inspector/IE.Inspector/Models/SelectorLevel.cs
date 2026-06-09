using System.Collections.ObjectModel;
using IE.Inspector.Helpers;
using IE.Inspector.Services;

namespace IE.Inspector.Models
{
    public class SelectorLevel : NotifyObject
    {
        private bool _isEnabled = true;
        private bool _canDisable = true;
        private string _tagLine = string.Empty;

        public SelectorLevel(string tagName)
        {
            TagName = tagName ?? "ctrl";
            Properties = new ObservableCollection<ElementPropertyItem>();
            RefreshTagLine();
        }

        public string TagName { get; }

        public string TagLine
        {
            get => _tagLine;
            private set => SetProperty(ref _tagLine, value);
        }

        public ObservableCollection<ElementPropertyItem> Properties { get; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (!CanDisable && !value)
                    return;

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
            TagLine = SelectorXmlSerializer.SerializeLevelTag(this);
        }
    }
}
