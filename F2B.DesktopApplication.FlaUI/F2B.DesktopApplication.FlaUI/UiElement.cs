using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using FlaUiMouseButton = FlaUI.Core.Input.MouseButton;

namespace F2B.DesktopApplication.FlaUI
{
    public sealed class UiElement
    {
        internal UiElement(AutomationElement element, UIA3Automation automation)
        {
            Element = element ?? throw new ArgumentNullException(nameof(element));
            Automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        public AutomationElement Element { get; }
        internal UIA3Automation Automation { get; }

        public string GetDisplayName()
        {
            try
            {
                var role = Element.Properties.ControlType.IsSupported
                    ? Element.Properties.ControlType.Value.ToString()
                    : "Element";
                var name = Element.Properties.Name.IsSupported
                    ? Element.Properties.Name.ValueOrDefault
                    : string.Empty;
                return string.IsNullOrEmpty(name) ? role : role + " '" + name + "'";
            }
            catch
            {
                return "Element";
            }
        }

        public string GetText()
        {
            try
            {
                if (Element.Patterns.Value.IsSupported)
                    return Element.Patterns.Value.Pattern.Value.Value ?? string.Empty;
            }
            catch
            {
            }

            try
            {
                if (Element.Patterns.Text.IsSupported)
                {
                    var ranges = Element.Patterns.Text.Pattern.GetVisibleRanges();
                    if (ranges != null && ranges.Length > 0)
                        return string.Concat(ranges.Select(r => r.GetText(-1)));
                }
            }
            catch
            {
            }

            try
            {
                if (Element.Properties.Name.IsSupported)
                    return Element.Properties.Name.ValueOrDefault ?? string.Empty;
            }
            catch
            {
            }

            return string.Empty;
        }

        public string GetProperty(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("Property name is required.", nameof(propertyName));

            switch (propertyName.Trim())
            {
                case "Name":
                    return Element.Properties.Name.ValueOrDefault ?? string.Empty;
                case "AutomationId":
                    return Element.Properties.AutomationId.ValueOrDefault ?? string.Empty;
                case "ClassName":
                    return Element.Properties.ClassName.ValueOrDefault ?? string.Empty;
                case "ControlType":
                    return Element.Properties.ControlType.Value.ToString();
                case "FrameworkId":
                    return Element.Properties.FrameworkId.ValueOrDefault ?? string.Empty;
                case "Text":
                    return GetText();
                case "IsEnabled":
                    return Element.Properties.IsEnabled.ValueOrDefault.ToString();
                case "IsOffscreen":
                    return Element.Properties.IsOffscreen.ValueOrDefault.ToString();
                case "ProcessId":
                    return Element.Properties.ProcessId.ValueOrDefault.ToString();
                default:
                    throw new ArgumentException("Unsupported property: " + propertyName);
            }
        }

        public bool IsEnabled()
        {
            try
            {
                return Element.Properties.IsEnabled.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }

        public bool IsChecked()
        {
            try
            {
                if (Element.Patterns.Toggle.IsSupported)
                    return Element.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On;

                var checkbox = Element.AsCheckBox();
                if (checkbox != null)
                    return checkbox.IsChecked.GetValueOrDefault();
            }
            catch
            {
            }

            return false;
        }

        public Rectangle GetBoundingRectangle()
        {
            try
            {
                return Element.BoundingRectangle;
            }
            catch
            {
                return Rectangle.Empty;
            }
        }

        public UiElement[] GetChildren()
        {
            try
            {
                return Element.FindAllChildren()
                    .Select(child => new UiElement(child, Automation))
                    .ToArray();
            }
            catch
            {
                return new UiElement[0];
            }
        }

        public UiElement GetParent()
        {
            try
            {
                var parent = Element.Parent;
                return parent == null ? null : new UiElement(parent, Automation);
            }
            catch
            {
                return null;
            }
        }

        public void Focus()
        {
            Element.Focus();
        }

        public void ScrollIntoView()
        {
            try
            {
                if (Element.Patterns.ScrollItem.IsSupported)
                {
                    Element.Patterns.ScrollItem.Pattern.ScrollIntoView();
                    return;
                }
            }
            catch
            {
            }

            Focus();
        }

        public void TakeScreenshot(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Output file path is required.", nameof(filePath));

            var rect = GetBoundingRectangle();
            if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
                throw new InvalidOperationException("Element has no valid bounding rectangle.");

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (var bitmap = new Bitmap(rect.Width, rect.Height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(rect.Location, Point.Empty, rect.Size);
                bitmap.Save(filePath);
            }
        }

        public void Click(MouseButton button = MouseButton.Left, int clickCount = 1)
        {
            Element.Focus();

            if (button == MouseButton.Left && clickCount == 1 && TryInvokePattern())
                return;

            var rect = GetBoundingRectangle();
            if (rect.IsEmpty)
                throw new InvalidOperationException("Element has no bounding rectangle.");

            var x = rect.X + rect.Width / 2;
            var y = rect.Y + rect.Height / 2;
            Mouse.MoveTo(new Point(x, y));

            switch (button)
            {
                case MouseButton.Left:
                    if (clickCount <= 1)
                        Mouse.Click();
                    else
                        Mouse.DoubleClick();
                    break;
                case MouseButton.Middle:
                    Mouse.Click(FlaUiMouseButton.Middle);
                    break;
                case MouseButton.Right:
                    Mouse.Click(FlaUiMouseButton.Right);
                    break;
            }
        }

        public void DoubleClick(MouseButton button = MouseButton.Left)
        {
            Click(button, clickCount: 2);
        }

        public void Check()
        {
            SetChecked(true);
        }

        public void Uncheck()
        {
            SetChecked(false);
        }

        public void Select(string text = null, string value = null, int? index = null, string textContains = null, string textRegex = null)
        {
            var comboBox = Element.AsComboBox();
            if (comboBox != null)
            {
                if (index.HasValue)
                {
                    comboBox.Select(index.Value);
                    return;
                }

                var comboItem = comboBox.Items.FirstOrDefault(item =>
                    MatchesOption(item.Text, text, value, textContains, textRegex));
                if (comboItem == null)
                    throw new InvalidOperationException("No matching option found.");

                comboBox.Select(comboItem.Text);
                return;
            }

            var listBox = Element.AsListBox();
            if (listBox != null)
            {
                if (index.HasValue)
                {
                    listBox.Select(index.Value);
                    return;
                }

                var listItem = listBox.Items.FirstOrDefault(item =>
                    MatchesOption(item.Text, text, value, textContains, textRegex));
                if (listItem == null)
                    throw new InvalidOperationException("No matching option found.");

                listBox.Select(listItem.Text);
                return;
            }

            throw new InvalidOperationException("Target element does not support selection.");
        }

        public string[] GetSelectedOptions()
        {
            var comboBox = Element.AsComboBox();
            if (comboBox != null)
            {
                var selected = comboBox.SelectedItem;
                return selected == null ? new string[0] : new[] { selected.Text ?? string.Empty };
            }

            var listBox = Element.AsListBox();
            if (listBox != null)
                return listBox.SelectedItems.Select(item => item.Text ?? string.Empty).ToArray();

            return new string[0];
        }

        public void InputText(string text, bool clearFirst = true)
        {
            text = text ?? string.Empty;
            Element.Focus();

            if (Element.Patterns.Value.IsSupported)
            {
                var valuePattern = Element.Patterns.Value.Pattern;
                if (clearFirst)
                    valuePattern.SetValue(string.Empty);
                valuePattern.SetValue(text);
                return;
            }

            if (clearFirst)
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);

            if (text.Length > 0)
                Keyboard.Type(text);
        }

        public void PressKeys(string keys)
        {
            if (string.IsNullOrEmpty(keys))
                return;

            Element.Focus();
            Keyboard.Type(keys);
        }

        private void SetChecked(bool desiredState)
        {
            if (Element.Patterns.Toggle.IsSupported)
            {
                var toggle = Element.Patterns.Toggle.Pattern;
                var current = toggle.ToggleState.Value == ToggleState.On;
                if (current != desiredState)
                    toggle.Toggle();
                return;
            }

            var checkbox = Element.AsCheckBox();
            if (checkbox != null)
            {
                checkbox.IsChecked = desiredState;
                return;
            }

            throw new InvalidOperationException("Target element does not support check/uncheck.");
        }

        private static bool MatchesOption(
            string itemText,
            string text,
            string value,
            string textContains,
            string textRegex)
        {
            itemText = itemText ?? string.Empty;

            if (!string.IsNullOrEmpty(text))
                return string.Equals(itemText, text, StringComparison.Ordinal);

            if (!string.IsNullOrEmpty(value))
                return string.Equals(itemText, value, StringComparison.Ordinal);

            if (!string.IsNullOrEmpty(textContains))
                return itemText.IndexOf(textContains, StringComparison.OrdinalIgnoreCase) >= 0;

            if (!string.IsNullOrEmpty(textRegex))
                return Regex.IsMatch(itemText, textRegex, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

            return false;
        }

        private bool TryInvokePattern()
        {
            try
            {
                if (!Element.Patterns.Invoke.IsSupported)
                    return false;

                Element.Patterns.Invoke.Pattern.Invoke();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
