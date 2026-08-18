using System;
using System.Activities;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Filters designer/ModelService hits that are Activities technically,
    /// but are expression wrappers / infra — not navigable workflow steps.
    /// </summary>
    internal static class ActivitySearchFilter
    {
        public static bool IsNavigableActivity(Type type)
        {
            if (type == null || !typeof(Activity).IsAssignableFrom(type))
            {
                return false;
            }

            if (typeof(DynamicActivity).IsAssignableFrom(type))
            {
                return false;
            }

            string fullName = type.FullName ?? string.Empty;
            string name = type.Name ?? string.Empty;
            int tick = name.IndexOf('`');
            string bare = tick > 0 ? name.Substring(0, tick) : name;

            if (string.Equals(bare, "ActivityBuilder", StringComparison.OrdinalIgnoreCase)
                || bare.StartsWith("ActivityBuilder", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Expression / literal / reference wrappers (appear under property editors).
            if (IsExpressionOrLiteralType(bare, fullName))
            {
                return false;
            }

            // Delegate hosts used inside TryCatch / ForEach, not droppable steps.
            if (bare.StartsWith("ActivityAction", StringComparison.OrdinalIgnoreCase)
                || bare.StartsWith("ActivityFunc", StringComparison.OrdinalIgnoreCase)
                || bare.StartsWith("ActivityDelegate", StringComparison.OrdinalIgnoreCase)
                || bare.StartsWith("InvokeAction", StringComparison.OrdinalIgnoreCase)
                || bare.StartsWith("InvokeFunc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(bare, "InvokeDelegate", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Generic Catch&lt;T&gt; metadata wrapper.
            if (string.Equals(bare, "Catch", StringComparison.OrdinalIgnoreCase) && type.IsGenericType)
            {
                return false;
            }

            return true;
        }

        public static bool IsNavigableDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return true;
            }

            string text = displayName.Trim();
            if (text.StartsWith("Literal<", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Literal`", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "Literal", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("VisualBasicValue", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("VisualBasicReference", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("CSharpValue", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("CSharpReference", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static bool IsExpressionOrLiteralType(string bareName, string fullName)
        {
            if (string.IsNullOrEmpty(bareName))
            {
                return true;
            }

            if (fullName.IndexOf("System.Activities.Expressions", StringComparison.OrdinalIgnoreCase) >= 0
                || fullName.IndexOf("Microsoft.VisualBasic.Activities", StringComparison.OrdinalIgnoreCase) >= 0
                || fullName.IndexOf("Microsoft.CSharp.Activities", StringComparison.OrdinalIgnoreCase) >= 0
                || fullName.IndexOf("System.Activities.XamlIntegration", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (string.Equals(bareName, "Literal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(bareName, "LambdaValue", StringComparison.OrdinalIgnoreCase)
                || string.Equals(bareName, "LambdaReference", StringComparison.OrdinalIgnoreCase)
                || string.Equals(bareName, "LocationReferenceValue", StringComparison.OrdinalIgnoreCase)
                || bareName.StartsWith("VisualBasic", StringComparison.OrdinalIgnoreCase)
                || bareName.StartsWith("CSharp", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // ArgumentValue`1, VariableValue`1, FieldValue`2, PropertyValue`2, ArrayItemValue`1, ...
            if (bareName.EndsWith("Value", StringComparison.Ordinal)
                && (bareName.IndexOf("Argument", StringComparison.OrdinalIgnoreCase) >= 0
                    || bareName.IndexOf("Variable", StringComparison.OrdinalIgnoreCase) >= 0
                    || bareName.IndexOf("Field", StringComparison.OrdinalIgnoreCase) >= 0
                    || bareName.IndexOf("Property", StringComparison.OrdinalIgnoreCase) >= 0
                    || bareName.IndexOf("ArrayItem", StringComparison.OrdinalIgnoreCase) >= 0
                    || bareName.IndexOf("DelegateArgument", StringComparison.OrdinalIgnoreCase) >= 0
                    || bareName.IndexOf("LocationReference", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            // ArgumentReference`1, VariableReference`1, ...
            if (bareName.EndsWith("Reference", StringComparison.Ordinal)
                && (bareName.IndexOf("Argument", StringComparison.OrdinalIgnoreCase) >= 0
                    || bareName.IndexOf("Variable", StringComparison.OrdinalIgnoreCase) >= 0
                    || bareName.IndexOf("Field", StringComparison.OrdinalIgnoreCase) >= 0
                    || bareName.IndexOf("Property", StringComparison.OrdinalIgnoreCase) >= 0
                    || bareName.IndexOf("ArrayItem", StringComparison.OrdinalIgnoreCase) >= 0
                    || bareName.IndexOf("DelegateArgument", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            return false;
        }
    }
}
