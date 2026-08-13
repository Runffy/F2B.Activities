using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using F2B.Forms.Model;

namespace F2B.Forms.Designers
{
    /// <summary>
    /// PropertyGrid dropdown with checkboxes for <see cref="FormControlTypeFilter"/>.
    /// </summary>
    public sealed class FormControlTypeFilterEditor : UITypeEditor
    {
        private static readonly FormControlTypeFilter[] SelectableFlags =
        {
            FormControlTypeFilter.Button,
            FormControlTypeFilter.Label,
            FormControlTypeFilter.TextBox,
            FormControlTypeFilter.TextArea,
            FormControlTypeFilter.CheckBox,
            FormControlTypeFilter.ComboBox,
            FormControlTypeFilter.DatePicker,
            FormControlTypeFilter.DateTimePicker,
            FormControlTypeFilter.CheckedListBox,
            FormControlTypeFilter.ListBox,
            FormControlTypeFilter.MaskedTextBox,
            FormControlTypeFilter.NumericUpDown,
            FormControlTypeFilter.PictureBox,
            FormControlTypeFilter.RadioButton,
            FormControlTypeFilter.Panel,
            FormControlTypeFilter.GroupBox,
            FormControlTypeFilter.ScrollContainer,
            FormControlTypeFilter.TableLayout,
            FormControlTypeFilter.DataGrid,
            FormControlTypeFilter.TabControl,
            FormControlTypeFilter.TabPage
        };

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.DropDown;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            if (provider == null)
            {
                return value;
            }

            var editorService = provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
            if (editorService == null)
            {
                return value;
            }

            var current = value is FormControlTypeFilter filter
                ? filter
                : FormControlTypeFilter.None;

            using (var list = new CheckedListBox())
            {
                list.BorderStyle = BorderStyle.None;
                list.CheckOnClick = true;
                list.IntegralHeight = false;
                list.Height = Math.Min(280, 22 * (SelectableFlags.Length + 2));

                list.Items.Add("(All / None)", current == FormControlTypeFilter.None || current == FormControlTypeFilter.All);

                foreach (FormControlTypeFilter flag in SelectableFlags)
                {
                    bool isChecked = current != FormControlTypeFilter.None
                        && current != FormControlTypeFilter.All
                        && (current & flag) == flag;
                    list.Items.Add(flag.ToString(), isChecked);
                }

                editorService.DropDownControl(list);

                if (list.GetItemChecked(0))
                {
                    return FormControlTypeFilter.None;
                }

                FormControlTypeFilter result = FormControlTypeFilter.None;
                for (int i = 1; i < list.Items.Count; i++)
                {
                    if (!list.GetItemChecked(i))
                    {
                        continue;
                    }

                    if (Enum.TryParse(Convert.ToString(list.Items[i]), out FormControlTypeFilter parsed))
                    {
                        result |= parsed;
                    }
                }

                return result;
            }
        }
    }
}
