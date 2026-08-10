using System;

namespace F2B.Forms.Model
{
    public static class FormControlType
    {
        public const string Button = "Button";
        public const string Label = "Label";
        public const string TextBox = "TextBox";
        public const string TextArea = "TextArea";
        public const string CheckBox = "CheckBox";
        public const string ComboBox = "ComboBox";
        public const string DatePicker = "DatePicker";
        public const string DateTimePicker = "DateTimePicker";
        public const string CheckedListBox = "CheckedListBox";
        public const string ListBox = "ListBox";
        public const string MaskedTextBox = "MaskedTextBox";
        public const string NumericUpDown = "NumericUpDown";
        public const string PictureBox = "PictureBox";
        public const string RadioButton = "RadioButton";
        public const string Panel = "Panel";
        public const string GroupBox = "GroupBox";
        public const string ScrollContainer = "ScrollContainer";
        public const string TableLayout = "TableLayout";
        public const string DataGrid = "DataGrid";
        public const string TabControl = "TabControl";
        public const string TabPage = "TabPage";
        public const string Form = "Form";

        public static bool IsKnown(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return false;
            }

            switch (type.Trim())
            {
                case Button:
                case Label:
                case TextBox:
                case TextArea:
                case CheckBox:
                case ComboBox:
                case DatePicker:
                case DateTimePicker:
                case CheckedListBox:
                case ListBox:
                case MaskedTextBox:
                case NumericUpDown:
                case PictureBox:
                case RadioButton:
                case Panel:
                case GroupBox:
                case ScrollContainer:
                case TableLayout:
                case DataGrid:
                case TabControl:
                case TabPage:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsListControl(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return false;
            }

            string t = type.Trim();
            return string.Equals(t, ListBox, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, CheckedListBox, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, ComboBox, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRadioButton(string type)
        {
            return !string.IsNullOrWhiteSpace(type)
                && string.Equals(type.Trim(), RadioButton, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsMaskedTextBox(string type)
        {
            return !string.IsNullOrWhiteSpace(type)
                && string.Equals(type.Trim(), MaskedTextBox, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsNumericUpDown(string type)
        {
            return !string.IsNullOrWhiteSpace(type)
                && string.Equals(type.Trim(), NumericUpDown, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPictureBox(string type)
        {
            return !string.IsNullOrWhiteSpace(type)
                && string.Equals(type.Trim(), PictureBox, StringComparison.OrdinalIgnoreCase);
        }
        public static bool IsDatePicker(string type)
        {
            return !string.IsNullOrWhiteSpace(type)
                && string.Equals(type.Trim(), DatePicker, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDateTimePicker(string type)
        {
            return !string.IsNullOrWhiteSpace(type)
                && string.Equals(type.Trim(), DateTimePicker, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>DatePicker or DateTimePicker (both map to WinForms DateTimePicker).</summary>
        public static bool IsDateControl(string type)
        {
            return IsDatePicker(type) || IsDateTimePicker(type);
        }

        public static bool IsScrollContainer(string type)
        {
            return !string.IsNullOrWhiteSpace(type)
                && string.Equals(type.Trim(), ScrollContainer, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTableLayout(string type)
        {
            return !string.IsNullOrWhiteSpace(type)
                && string.Equals(type.Trim(), TableLayout, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDataGrid(string type)
        {
            return !string.IsNullOrWhiteSpace(type)
                && string.Equals(type.Trim(), DataGrid, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>May own nested normal controls (Panel / GroupBox / TabPage / ScrollContainer / TableLayout).</summary>
        public static bool IsContainer(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return false;
            }

            string t = type.Trim();
            return string.Equals(t, Panel, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, GroupBox, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, TabPage, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, ScrollContainer, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, TableLayout, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTabControl(string type)
        {
            return !string.IsNullOrWhiteSpace(type)
                && string.Equals(type.Trim(), TabControl, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTabPage(string type)
        {
            return !string.IsNullOrWhiteSpace(type)
                && string.Equals(type.Trim(), TabPage, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Anything that nests children in JSON (containers + TabControl).</summary>
        public static bool HasChildControls(string type)
        {
            return IsContainer(type) || IsTabControl(type);
        }
    }
}
