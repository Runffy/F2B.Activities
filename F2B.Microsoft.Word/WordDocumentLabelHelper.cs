using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    /// <summary>
    /// Applies an organization document label before save.
    /// Prefers Office SensitivityLabel COM API (Microsoft 365); falls back to custom property.
    /// </summary>
    internal static class WordDocumentLabelHelper
    {
        private const string FallbackPropertyName = "DocumentLabel";

        internal static string ToDisplayName(WordDocumentLabel label)
        {
            switch (label)
            {
                case WordDocumentLabel.Public:
                    return "Public";
                case WordDocumentLabel.Internal:
                    return "Internal";
                case WordDocumentLabel.Confidential:
                    return "Confidential";
                case WordDocumentLabel.Restricted:
                    return "Restricted";
                case WordDocumentLabel.None:
                default:
                    return null;
            }
        }

        internal static void Apply(InteropWord.Document document, WordDocumentLabel label)
        {
            if (document == null || label == WordDocumentLabel.None)
            {
                return;
            }

            string displayName = ToDisplayName(label);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return;
            }

            if (TryApplySensitivityLabel(document, displayName))
            {
                return;
            }

            SetOrAddCustomProperty(document, FallbackPropertyName, displayName);
        }

        private static bool TryApplySensitivityLabel(InteropWord.Document document, string displayName)
        {
            try
            {
                object sensitivity = GetPropertyValue(document, "SensitivityLabel");
                if (sensitivity == null)
                {
                    return false;
                }

                object labels = InvokeMember(sensitivity, "GetLabels", BindingFlags.InvokeMethod, null);
                object matched = FindLabelByName(labels, displayName);
                if (matched == null)
                {
                    return false;
                }

                // SetLabel(labelInfo, assignmentMethod) — assignmentMethod may be optional/empty on some hosts.
                try
                {
                    InvokeMember(sensitivity, "SetLabel", BindingFlags.InvokeMethod, new object[] { matched, string.Empty });
                }
                catch
                {
                    InvokeMember(sensitivity, "SetLabel", BindingFlags.InvokeMethod, new object[] { matched });
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object FindLabelByName(object labels, string displayName)
        {
            if (labels == null)
            {
                return null;
            }

            if (labels is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    if (LabelNameMatches(item, displayName))
                    {
                        return item;
                    }
                }
            }

            // Some Office hosts expose Count + Item(index) instead of IEnumerable.
            object countObj = GetPropertyValue(labels, "Count", "Length");
            int count;
            if (countObj != null && int.TryParse(Convert.ToString(countObj, CultureInfo.InvariantCulture), out count))
            {
                for (int i = 1; i <= count; i++)
                {
                    object item = null;
                    try
                    {
                        item = InvokeMember(labels, "Item", BindingFlags.InvokeMethod | BindingFlags.GetProperty, new object[] { i });
                    }
                    catch
                    {
                        try
                        {
                            item = InvokeMember(labels, "Item", BindingFlags.InvokeMethod | BindingFlags.GetProperty, new object[] { i - 1 });
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    if (item != null && LabelNameMatches(item, displayName))
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        private static bool LabelNameMatches(object labelInfo, string displayName)
        {
            string[] names =
            {
                GetStringProperty(labelInfo, "Name"),
                GetStringProperty(labelInfo, "DisplayName"),
                GetStringProperty(labelInfo, "Tooltip"),
                GetStringProperty(labelInfo, "Id")
            };

            foreach (string name in names)
            {
                if (!string.IsNullOrWhiteSpace(name)
                    && string.Equals(name.Trim(), displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Partial match: org labels often look like "Internal Use Only" / "HSBC Internal".
            foreach (string name in names)
            {
                if (!string.IsNullOrWhiteSpace(name)
                    && name.IndexOf(displayName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetOrAddCustomProperty(InteropWord.Document document, string propertyName, string value)
        {
            object properties = null;
            try
            {
                properties = document.CustomDocumentProperties;
                if (TrySetExistingCustomProperty(properties, propertyName, value))
                {
                    return;
                }

                // Add(Name, LinkToContent, Type, Value) — msoPropertyTypeString = 4
                InvokeMember(
                    properties,
                    "Add",
                    BindingFlags.InvokeMethod,
                    new object[] { propertyName, false, 4, value });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to apply document label '" + value + "'. " +
                    "Sensitivity Label API was unavailable and custom property fallback failed: " + ex.Message,
                    ex);
            }
            finally
            {
                if (properties != null)
                {
                    WordCom.ReleaseComObject(properties);
                }
            }
        }

        private static bool TrySetExistingCustomProperty(object properties, string propertyName, string value)
        {
            try
            {
                object existing = InvokeMember(
                    properties,
                    "Item",
                    BindingFlags.InvokeMethod | BindingFlags.GetProperty,
                    new object[] { propertyName });
                if (existing == null)
                {
                    return false;
                }

                try
                {
                    SetPropertyValue(existing, "Value", value);
                }
                finally
                {
                    WordCom.ReleaseComObject(existing);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object GetPropertyValue(object target, params string[] names)
        {
            if (target == null || names == null)
            {
                return null;
            }

            foreach (string name in names)
            {
                try
                {
                    return target.GetType().InvokeMember(
                        name,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty,
                        null,
                        target,
                        null);
                }
                catch
                {
                    // try next
                }
            }

            return null;
        }

        private static string GetStringProperty(object target, string name)
        {
            object value = GetPropertyValue(target, name);
            return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static void SetPropertyValue(object target, string name, object value)
        {
            target.GetType().InvokeMember(
                name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty,
                null,
                target,
                new[] { value });
        }

        private static object InvokeMember(object target, string name, BindingFlags flags, object[] args)
        {
            return target.GetType().InvokeMember(
                name,
                BindingFlags.Public | BindingFlags.Instance | flags,
                null,
                target,
                args);
        }
    }
}
