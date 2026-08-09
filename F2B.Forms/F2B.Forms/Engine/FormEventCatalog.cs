using System;
using System.Collections.Generic;
using F2B.Forms.Model;

namespace F2B.Forms.Engine
{
    public static class FormEventCatalog
    {
        public static IList<string> GetEventsForControlType(string controlType)
        {
            var list = new List<string>();
            string type = controlType == null ? string.Empty : controlType.Trim();

            // Form lifecycle is not bindable — use AsyncForm Init Scope / Close Scope.
            if (string.Equals(type, FormControlType.Form, StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "form", StringComparison.OrdinalIgnoreCase))
            {
                return list;
            }

            switch (type)
            {
                case FormControlType.Button:
                    list.Add("Click");
                    break;

                case FormControlType.CheckBox:
                    list.Add("Check");
                    list.Add("Uncheck");
                    break;

                case FormControlType.ComboBox:
                    list.Add("Change");
                    break;

                case FormControlType.DatePicker:
                case FormControlType.DateTimePicker:
                    list.Add("Change");
                    break;

                case FormControlType.TextBox:
                case FormControlType.TextArea:
                    list.Add("KeyUp");
                    list.Add("Focus");
                    list.Add("Blur");
                    list.Add("TextChanged");
                    break;

                case FormControlType.TabControl:
                    list.Add("Change");
                    break;

                case FormControlType.TabPage:
                    // Enter = 切入该页, Leave = 切出该页
                    list.Add("Enter");
                    list.Add("Leave");
                    break;

                case FormControlType.DataGrid:
                    list.Add("SelectionChanged");
                    list.Add("CellClick");
                    break;

                case FormControlType.Label:
                case FormControlType.Panel:
                case FormControlType.GroupBox:
                case FormControlType.ScrollContainer:
                case FormControlType.TableLayout:
                    // Containers / static text — bind interactions on child controls instead.
                    break;

                default:
                    break;
            }

            return list;
        }

        /// <summary>Control types that expose at least one bindable event (for Control Type dropdown).</summary>
        public static IList<string> GetBindableControlTypes()
        {
            return new List<string>
            {
                FormControlType.Button,
                FormControlType.CheckBox,
                FormControlType.ComboBox,
                FormControlType.DatePicker,
                FormControlType.DateTimePicker,
                FormControlType.TextBox,
                FormControlType.TextArea,
                FormControlType.TabControl,
                FormControlType.TabPage,
                FormControlType.DataGrid
            };
        }

        public static List<ControlRef> CollectControls(FormDefinition definition)
        {
            // Do not include form — Closing is AsyncForm Close Scope; post-close logic goes after AsyncForm.
            var result = new List<ControlRef>();

            if (definition != null)
            {
                CollectControls(definition.Controls, result);
            }

            return result;
        }

        private static void CollectControls(IEnumerable<ControlDefinition> controls, List<ControlRef> result)
        {
            if (controls == null)
            {
                return;
            }

            foreach (ControlDefinition control in controls)
            {
                if (control == null || string.IsNullOrWhiteSpace(control.Id))
                {
                    continue;
                }

                // Only bindable controls (those with at least one event) appear in Bind Event Control Id.
                if (GetEventsForControlType(control.Type).Count > 0)
                {
                    string id = control.Id.Trim();
                    result.Add(new ControlRef
                    {
                        Id = id,
                        Type = control.Type,
                        // Dropdown shows bare id; type is a separate field.
                        Display = id
                    });
                }

                if (FormControlType.HasChildControls(control.Type))
                {
                    CollectControls(control.Controls, result);
                }
            }
        }

        public sealed class ControlRef
        {
            public string Id { get; set; }
            public string Type { get; set; }
            public string Display { get; set; }

            public override string ToString()
            {
                return Display ?? Id;
            }
        }
    }
}
