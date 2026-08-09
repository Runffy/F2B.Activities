using System;
using System.IO;
using F2B.Forms.Model;
using Newtonsoft.Json;

namespace F2B.Forms.Engine
{
    public static class FormJsonLoader
    {
        public static FormDefinition LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("FormPath is required.", nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Form JSON file was not found.", path);
            }

            string json = File.ReadAllText(path);
            return LoadFromJson(json);
        }

        public static FormDefinition LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Form JSON is empty.", nameof(json));
            }

            FormDefinition definition = JsonConvert.DeserializeObject<FormDefinition>(json);
            if (definition == null)
            {
                throw new InvalidOperationException("Failed to deserialize form JSON.");
            }

            if (definition.Controls == null)
            {
                definition.Controls = new System.Collections.Generic.List<ControlDefinition>();
            }

            Validate(definition);
            return definition;
        }

        public static string ToJson(FormDefinition definition)
        {
            return JsonConvert.SerializeObject(definition, Formatting.Indented);
        }

        private static void Validate(FormDefinition definition)
        {
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ValidateControls(definition.Controls, ids, "root", parentType: null);
        }

        private static void ValidateControls(
            System.Collections.Generic.List<ControlDefinition> controls,
            System.Collections.Generic.HashSet<string> ids,
            string parentPath,
            string parentType)
        {
            if (controls == null)
            {
                return;
            }

            for (int i = 0; i < controls.Count; i++)
            {
                ControlDefinition control = controls[i];
                if (control == null)
                {
                    throw new InvalidOperationException("Control at " + parentPath + "[" + i + "] is null.");
                }

                if (string.IsNullOrWhiteSpace(control.Id))
                {
                    throw new InvalidOperationException("Control at " + parentPath + "[" + i + "] is missing id.");
                }

                if (!FormControlType.IsKnown(control.Type))
                {
                    throw new InvalidOperationException(
                        "Control '" + control.Id + "' has unsupported type '" + control.Type + "'.");
                }

                if (FormControlType.IsTabPage(control.Type) && !FormControlType.IsTabControl(parentType))
                {
                    throw new InvalidOperationException(
                        "TabPage '" + control.Id + "' must be a child of TabControl.");
                }

                if (!ids.Add(control.Id.Trim()))
                {
                    throw new InvalidOperationException("Duplicate control id: '" + control.Id + "'.");
                }

                if (FormControlType.IsTabControl(control.Type))
                {
                    ValidateTabControlChildren(control);
                    ValidateControls(control.Controls, ids, control.Id, control.Type);
                }
                else if (FormControlType.IsContainer(control.Type))
                {
                    ValidateControls(control.Controls, ids, control.Id, control.Type);
                }
            }
        }

        private static void ValidateTabControlChildren(ControlDefinition tabControl)
        {
            if (tabControl.Controls == null || tabControl.Controls.Count == 0)
            {
                throw new InvalidOperationException(
                    "TabControl '" + tabControl.Id + "' must contain at least one TabPage.");
            }

            for (int i = 0; i < tabControl.Controls.Count; i++)
            {
                ControlDefinition page = tabControl.Controls[i];
                if (page == null || !FormControlType.IsTabPage(page.Type))
                {
                    throw new InvalidOperationException(
                        "TabControl '" + tabControl.Id + "' child[" + i + "] must be type TabPage.");
                }
            }
        }
    }
}
