using System;
using System.Windows.Forms;
using F2B.Forms.Model;

namespace F2B.Forms.Engine
{
    /// <summary>
    /// Maps a live WinForms control back to the F2B Forms type name.
    /// </summary>
    internal static class FormControlTypeResolver
    {
        internal static string Resolve(Control control)
        {
            if (control == null)
            {
                return null;
            }

            if (control is Form)
            {
                return FormControlType.Form;
            }

            if (control is Button)
            {
                return FormControlType.Button;
            }

            if (control is Label)
            {
                return FormControlType.Label;
            }

            if (control is CheckBox)
            {
                return FormControlType.CheckBox;
            }

            if (control is RadioButton)
            {
                return FormControlType.RadioButton;
            }

            if (control is ComboBox)
            {
                return FormControlType.ComboBox;
            }

            if (control is CheckedListBox)
            {
                return FormControlType.CheckedListBox;
            }

            if (control is ListBox)
            {
                return FormControlType.ListBox;
            }

            if (control is MaskedTextBox)
            {
                return FormControlType.MaskedTextBox;
            }

            if (control is NumericUpDown)
            {
                return FormControlType.NumericUpDown;
            }

            if (control is PictureBox)
            {
                return FormControlType.PictureBox;
            }

            if (control is DateTimePicker dateTimePicker)
            {
                string tag = dateTimePicker.Tag as string;
                if (string.Equals(tag, FormControlType.DatePicker, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tag, FormControlType.DateTimePicker, StringComparison.OrdinalIgnoreCase))
                {
                    return tag;
                }

                return FormControlType.DateTimePicker;
            }

            if (control is DataGridView)
            {
                return FormControlType.DataGrid;
            }

            if (control is TableLayoutPanel)
            {
                return FormControlType.TableLayout;
            }

            if (control is TabControl)
            {
                return FormControlType.TabControl;
            }

            if (control is TabPage)
            {
                return FormControlType.TabPage;
            }

            if (control is GroupBox)
            {
                return FormControlType.GroupBox;
            }

            if (control is Panel panel)
            {
                string tag = panel.Tag as string;
                if (string.Equals(tag, FormControlType.ScrollContainer, StringComparison.OrdinalIgnoreCase))
                {
                    return FormControlType.ScrollContainer;
                }

                if (string.Equals(tag, FormControlType.Panel, StringComparison.OrdinalIgnoreCase))
                {
                    return FormControlType.Panel;
                }

                return panel.AutoScroll ? FormControlType.ScrollContainer : FormControlType.Panel;
            }

            if (control is TextBox textBox)
            {
                // TextArea is Multiline + AcceptsReturn; designed single-line TextBox uses Multiline without AcceptsReturn.
                if (textBox.Multiline && textBox.AcceptsReturn)
                {
                    return FormControlType.TextArea;
                }

                return FormControlType.TextBox;
            }

            return control.GetType().Name;
        }
    }
}
