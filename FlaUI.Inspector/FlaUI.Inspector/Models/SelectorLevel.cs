using System.Collections.ObjectModel;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Inspector.Helpers;
using FlaUI.Inspector.Services;

namespace FlaUI.Inspector.Models
{
    public class SelectorLevel : NotifyObject
    {
        private bool _isEnabled = true;
        private bool _canDisable = true;
        private string _tagLine = string.Empty;

        public SelectorLevel(AutomationElement element)
        {
            Element = element;
            TagName = GetTagName(element);
            Properties = new ObservableCollection<ElementPropertyItem>();
            RefreshTagLine();
        }

        public SelectorLevel(string tagName)
        {
            TagName = tagName ?? "ctrl";
            Properties = new ObservableCollection<ElementPropertyItem>();
            RefreshTagLine();
        }

        public AutomationElement Element { get; }
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

        private static string GetTagName(AutomationElement element)
        {
            try
            {
                if (element.Properties.ControlType.IsSupported &&
                    element.Properties.ControlType.Value == ControlType.Window)
                    return "wnd";
            }
            catch
            {
            }

            return "ctrl";
        }
    }
}
