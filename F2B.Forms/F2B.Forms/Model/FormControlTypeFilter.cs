using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace F2B.Forms.Model
{
    /// <summary>
    /// Multi-select filter for Get Child Controls (PropertyGrid shows checkboxes via Flags).
    /// <see cref="None"/> means no filter (include all types).
    /// </summary>
    [Flags]
    [TypeConverter(typeof(FormControlTypeFilterConverter))]
    public enum FormControlTypeFilter : long
    {
        None = 0,

        Button = 1L << 0,
        Label = 1L << 1,
        TextBox = 1L << 2,
        TextArea = 1L << 3,
        CheckBox = 1L << 4,
        ComboBox = 1L << 5,
        DatePicker = 1L << 6,
        DateTimePicker = 1L << 7,
        CheckedListBox = 1L << 8,
        ListBox = 1L << 9,
        MaskedTextBox = 1L << 10,
        NumericUpDown = 1L << 11,
        PictureBox = 1L << 12,
        RadioButton = 1L << 13,
        Panel = 1L << 14,
        GroupBox = 1L << 15,
        ScrollContainer = 1L << 16,
        TableLayout = 1L << 17,
        DataGrid = 1L << 18,
        TabControl = 1L << 19,
        TabPage = 1L << 20,

        All = Button | Label | TextBox | TextArea | CheckBox | ComboBox
            | DatePicker | DateTimePicker | CheckedListBox | ListBox | MaskedTextBox
            | NumericUpDown | PictureBox | RadioButton | Panel | GroupBox
            | ScrollContainer | TableLayout | DataGrid | TabControl | TabPage
    }

    /// <summary>
    /// Maps filter flags ↔ FormControlType name strings.
    /// </summary>
    public static class FormControlTypeFilterUtil
    {
        private static readonly KeyValuePair<FormControlTypeFilter, string>[] Map =
        {
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.Button, FormControlType.Button),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.Label, FormControlType.Label),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.TextBox, FormControlType.TextBox),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.TextArea, FormControlType.TextArea),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.CheckBox, FormControlType.CheckBox),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.ComboBox, FormControlType.ComboBox),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.DatePicker, FormControlType.DatePicker),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.DateTimePicker, FormControlType.DateTimePicker),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.CheckedListBox, FormControlType.CheckedListBox),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.ListBox, FormControlType.ListBox),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.MaskedTextBox, FormControlType.MaskedTextBox),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.NumericUpDown, FormControlType.NumericUpDown),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.PictureBox, FormControlType.PictureBox),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.RadioButton, FormControlType.RadioButton),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.Panel, FormControlType.Panel),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.GroupBox, FormControlType.GroupBox),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.ScrollContainer, FormControlType.ScrollContainer),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.TableLayout, FormControlType.TableLayout),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.DataGrid, FormControlType.DataGrid),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.TabControl, FormControlType.TabControl),
            new KeyValuePair<FormControlTypeFilter, string>(FormControlTypeFilter.TabPage, FormControlType.TabPage)
        };

        public static HashSet<string> ToTypeNameSet(FormControlTypeFilter filter)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (filter == FormControlTypeFilter.None || filter == FormControlTypeFilter.All)
            {
                return set; // empty = no filter / all
            }

            foreach (KeyValuePair<FormControlTypeFilter, string> pair in Map)
            {
                if ((filter & pair.Key) == pair.Key)
                {
                    set.Add(pair.Value);
                }
            }

            return set;
        }

        public static bool PassesFilter(FormControlTypeFilter filter, string typeName)
        {
            if (filter == FormControlTypeFilter.None || filter == FormControlTypeFilter.All)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            HashSet<string> allowed = ToTypeNameSet(filter);
            return allowed.Count == 0 || allowed.Contains(typeName.Trim());
        }

        /// <summary>
        /// Optional runtime string[] filter (expression). Empty / null = no extra filter.
        /// Combined with flags: must pass both when flags are selective.
        /// </summary>
        public static bool PassesStringFilter(string[] typeFilter, string typeName)
        {
            if (typeFilter == null || typeFilter.Length == 0)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            string target = typeName.Trim();
            foreach (string entry in typeFilter)
            {
                if (!string.IsNullOrWhiteSpace(entry)
                    && string.Equals(entry.Trim(), target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Ensures Flags enum expands to checkboxes in PropertyGrid (WF / OpenRPA).
    /// </summary>
    public sealed class FormControlTypeFilterConverter : EnumConverter
    {
        public FormControlTypeFilterConverter()
            : base(typeof(FormControlTypeFilter))
        {
        }
    }
}
