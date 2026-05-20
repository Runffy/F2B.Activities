using System;
using System.Collections.Generic;
using System.Reflection;

namespace F2B.Browser.IExplore.Com
{
    internal static class HtmlElementDomHelper
    {
        public static string GetTagName(object element)
        {
            try
            {
                dynamic el = element;
                return ((string)el.tagName ?? string.Empty).ToUpperInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string GetInputType(object element)
        {
            try
            {
                dynamic el = element;
                return ((string)el.type ?? string.Empty).ToLowerInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void SetChecked(object element, bool isChecked)
        {
            EnsureCheckable(element);
            try
            {
                var inputType = GetInputType(element);
                var current = ReadCheckedState(element);

                if (inputType == "checkbox")
                {
                    if (current != isChecked)
                        ClickElement(element);
                }
                else if (isChecked && !current)
                {
                    ClickElement(element);
                }

                ApplyCheckedState(element, isChecked);

                if (ReadCheckedState(element) != isChecked && inputType == "checkbox")
                {
                    if (ReadCheckedState(element) != isChecked)
                        ClickElement(element);
                    ApplyCheckedState(element, isChecked);
                }

                FireChange(element);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("SetChecked failed: " + ex.Message, ex);
            }
        }

        public static bool IsChecked(object element)
        {
            EnsureCheckable(element);
            try
            {
                return ReadCheckedState(element);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("IsChecked failed: " + ex.Message, ex);
            }
        }

        public static void SelectOptions(object element, IList<SelectCriterion> criteria)
        {
            if (criteria == null || criteria.Count == 0)
                throw new ArgumentException("At least one option criterion is required.", nameof(criteria));

            if (!string.Equals(GetTagName(element), "SELECT", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Select is only supported on &lt;select&gt; elements.");

            try
            {
                dynamic sel = element;
                bool multiple = sel.multiple;

                if (!multiple && criteria.Count > 1)
                {
                    throw new InvalidOperationException(
                        "Single-select &lt;select&gt; cannot select multiple options in one call. Use a multiple select or call Select once per option.");
                }

                if (!multiple)
                    ClearAllOptions(sel);

                var selected = new HashSet<int>();
                foreach (var criterion in criteria)
                {
                    int optionIndex = FindOptionIndex(sel, criterion);
                    if (!selected.Add(optionIndex))
                        continue;

                    dynamic option = sel.options.item(optionIndex);
                    option.selected = true;
                }

                FireChange(sel);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Select failed: " + ex.Message, ex);
            }
        }

        public static string GetText(object element)
        {
            if (!ComElementHelper.IsValidElement(element))
                return string.Empty;

            try
            {
                dynamic el = element;
                var tag = GetTagName(element);
                if (tag == "INPUT" || tag == "TEXTAREA")
                {
                    object v = el.value;
                    return v == null || v is DBNull ? string.Empty : v.ToString();
                }

                object text = el.innerText;
                if (text == null || text is DBNull)
                    return string.Empty;
                return text.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("GetText failed: " + ex.Message, ex);
            }
        }

        public static string GetValue(object element)
        {
            if (!ComElementHelper.IsValidElement(element))
                return string.Empty;

            try
            {
                dynamic el = element;
                object v = el.value;
                return v == null || v is DBNull ? string.Empty : v.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("GetValue failed: " + ex.Message, ex);
            }
        }

        public static string GetAttribute(object element, string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
                throw new ArgumentException("Attribute name is required.", nameof(attributeName));

            if (!ComElementHelper.IsValidElement(element))
                return string.Empty;

            try
            {
                dynamic el = element;
                object v = el.getAttribute(attributeName);
                if (v == null || v is DBNull)
                    return string.Empty;
                return v.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("GetAttribute failed: " + ex.Message, ex);
            }
        }

        private static void EnsureCheckable(object element)
        {
            if (!ComElementHelper.IsValidElement(element))
                throw new InvalidOperationException("Element was not found (MSHTML returned null/DBNull).");

            var tag = GetTagName(element);
            if (!tag.Equals("INPUT", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Check/Uncheck/IsChecked require &lt;input type=\"checkbox\"&gt; or &lt;input type=\"radio\"&gt;.");
            }

            var type = GetInputType(element);
            if (type != "checkbox" && type != "radio")
            {
                throw new InvalidOperationException(
                    "Check/Uncheck/IsChecked require input type checkbox or radio, got: " + type);
            }
        }

        private static void ClearAllOptions(dynamic select)
        {
            int count = select.options.length;
            for (int i = 0; i < count; i++)
                select.options.item(i).selected = false;
        }

        private static int FindOptionIndex(dynamic select, SelectCriterion criterion)
        {
            int count = select.options.length;
            for (int i = 0; i < count; i++)
            {
                dynamic option = select.options.item(i);
                if (MatchesCriterion(option, i, criterion))
                    return i;
            }

            throw new InvalidOperationException(DescribeCriterion(criterion));
        }

        private static bool MatchesCriterion(dynamic option, int index, SelectCriterion criterion)
        {
            if (criterion.Index.HasValue && string.IsNullOrEmpty(criterion.Text) && string.IsNullOrEmpty(criterion.Value))
                return criterion.Index.Value == index;

            if (criterion.Index.HasValue && criterion.Index.Value != index)
                return false;

            string text = null;
            string value = null;
            try { text = (string)option.text; } catch { /* ignore */ }
            try { value = (string)option.value; } catch { /* ignore */ }

            if (!string.IsNullOrEmpty(criterion.Text)
                && (text ?? string.Empty).IndexOf(criterion.Text, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (!string.IsNullOrEmpty(criterion.Value)
                && !string.Equals(value ?? string.Empty, criterion.Value, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private static string DescribeCriterion(SelectCriterion c)
        {
            if (c.Index.HasValue)
                return "No &lt;option&gt; matched index=" + c.Index.Value;
            if (!string.IsNullOrEmpty(c.Text))
                return "No &lt;option&gt; matched text=\"" + c.Text + "\"";
            if (!string.IsNullOrEmpty(c.Value))
                return "No &lt;option&gt; matched value=\"" + c.Value + "\"";
            return "No matching option.";
        }

        private static void FireChange(object element)
        {
            try
            {
                dynamic el = element;
                el.fireEvent("onchange");
            }
            catch { /* ignore */ }
        }

        private static void ClickElement(object element)
        {
            dynamic el = element;
            el.click();
        }

        private static void ApplyCheckedState(object element, bool isChecked)
        {
            SetBoolProperty(element, "checked", isChecked);
            dynamic el = element;
            if (isChecked)
                el.setAttribute("checked", "checked");
            else
                el.removeAttribute("checked");
        }

        private static bool ReadCheckedState(object element)
        {
            if (TryGetBoolProperty(element, "checked", out var propValue) && propValue)
                return true;

            try
            {
                dynamic el = element;
                object attr = el.getAttribute("checked");
                if (attr == null || attr is DBNull)
                    return false;

                if (attr is bool b)
                    return b;

                var s = attr.ToString();
                if (string.IsNullOrEmpty(s))
                    return true;

                return s.Equals("checked", StringComparison.OrdinalIgnoreCase)
                    || s.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return propValue;
            }
        }

        private static void SetBoolProperty(object element, string propertyName, bool value)
        {
            element.GetType().InvokeMember(
                propertyName,
                BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.Instance,
                null,
                element,
                new object[] { value });
        }

        private static bool TryGetBoolProperty(object element, string propertyName, out bool value)
        {
            value = false;
            try
            {
                var raw = element.GetType().InvokeMember(
                    propertyName,
                    BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    element,
                    null);

                if (raw == null || raw is DBNull)
                    return true;

                if (raw is bool b)
                {
                    value = b;
                    return true;
                }

                value = Convert.ToBoolean(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
