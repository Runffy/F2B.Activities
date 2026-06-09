using System;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace F2B.DesktopApplication.FlaUI
{
    public sealed class UiWindow
    {
        internal UiWindow(AutomationElement element, UIA3Automation automation)
        {
            Element = element ?? throw new ArgumentNullException(nameof(element));
            Automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        public AutomationElement Element { get; }
        internal UIA3Automation Automation { get; }

        public string GetTitle()
        {
            try
            {
                return Element.Properties.Name.IsSupported
                    ? Element.Properties.Name.ValueOrDefault ?? string.Empty
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public void Activate()
        {
            try
            {
                Element.AsWindow()?.SetForeground();
            }
            catch
            {
                Element.Focus();
            }
        }

        public void Close()
        {
            try
            {
                if (Element.Patterns.Window.IsSupported)
                {
                    Element.Patterns.Window.Pattern.Close();
                    return;
                }
            }
            catch
            {
            }

            Element.AsWindow()?.Close();
        }

        internal UiElement AsElement()
        {
            return new UiElement(Element, Automation);
        }
    }
}
