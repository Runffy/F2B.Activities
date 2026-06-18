using System;
using System.Activities;
using System.Activities.Expressions;
using System.ComponentModel;
using System.Reflection;

namespace F2B.Browser.Chromium.Bridge
{
    internal static class BridgeActivityArgumentHelper
    {
        public static T GetOrDefault<T>(InArgument<T> argument, CodeActivityContext context, T fallback)
        {
            if (argument == null || argument.Expression == null)
                return fallback;

            return argument.Get(context);
        }

        public static bool HasExpression(Argument argument)
        {
            return argument != null && argument.Expression != null;
        }

        public static BwElement GetBwElement(InArgument<BwElement> argument, CodeActivityContext context)
        {
            if (!HasExpression(argument))
                return null;

            try
            {
                var direct = argument.Get(context);
                if (direct != null)
                    return direct;
            }
            catch
            {
                // Fall back when the bound expression cannot be evaluated normally.
            }

            var variableName = ExtractVariableName(argument.Expression);
            if (string.IsNullOrWhiteSpace(variableName))
                return null;

            return AsBwElement(ResolveWorkflowVariable(context, variableName));
        }

        public static void SetBwElement(OutArgument<BwElement> argument, ActivityContext context, BwElement value)
        {
            if (argument == null || !HasExpression(argument))
                return;

            argument.Set(context, value);
        }

        public static BwElement AsBwElement(object value)
        {
            if (value == null)
                return null;

            if (value is BwElement element)
                return element;

            throw new ArgumentException(
                "Expected " + typeof(BwElement).FullName + ", but received " + value.GetType().FullName + ".");
        }

        public static string TryGetBoundVariableName(Argument argument)
        {
            if (argument == null || argument.Expression == null)
                return string.Empty;

            return ExtractVariableName(argument.Expression) ?? argument.Expression.ToString();
        }

        private static string ExtractVariableName(Activity expression)
        {
            if (expression == null)
                return null;

            var textExpression = expression as ITextExpression;
            if (textExpression != null)
            {
                var fromText = NormalizeVariableName(textExpression.ExpressionText);
                if (!string.IsNullOrWhiteSpace(fromText))
                    return fromText;
            }

            var expressionType = expression.GetType();
            foreach (var propertyName in new[] { "VariableName", "ExpressionText", "Name" })
            {
                var property = expressionType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null || property.PropertyType != typeof(string))
                    continue;

                var fromProperty = NormalizeVariableName(property.GetValue(expression) as string);
                if (!string.IsNullOrWhiteSpace(fromProperty))
                    return fromProperty;
            }

            return NormalizeVariableName(expression.ToString());
        }

        private static string NormalizeVariableName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            text = text.Trim();
            if (text.StartsWith("[", StringComparison.Ordinal) && text.EndsWith("]", StringComparison.Ordinal))
                text = text.Substring(1, text.Length - 2).Trim();

            if (text.Length >= 2 && text.StartsWith("\"", StringComparison.Ordinal) && text.EndsWith("\"", StringComparison.Ordinal))
                text = text.Substring(1, text.Length - 2).Trim();

            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (text.IndexOfAny(new[] { ' ', '.', '(', ')', '+', '-', '*', '/', '&', '|', '=', '<', '>' }) >= 0)
                return null;

            return text;
        }

        private static object ResolveWorkflowVariable(ActivityContext context, string variableName)
        {
            variableName = NormalizeVariableName(variableName);
            if (string.IsNullOrWhiteSpace(variableName))
                return null;

            var dataContext = context.DataContext;
            if (dataContext != null)
            {
                foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(dataContext))
                {
                    if (string.Equals(descriptor.Name, variableName, StringComparison.OrdinalIgnoreCase))
                        return descriptor.GetValue(dataContext);
                }
            }

            return TryGetOpenRpaVariable(context, variableName);
        }

        private static object TryGetOpenRpaVariable(ActivityContext context, string variableName)
        {
            try
            {
                var workflowInstanceType = Type.GetType("OpenRPA.WorkflowInstance, OpenRPA", false);
                if (workflowInstanceType == null)
                    return null;

                var instancesProperty = workflowInstanceType.GetProperty("Instances", BindingFlags.Public | BindingFlags.Static);
                var instances = instancesProperty?.GetValue(null) as System.Collections.IEnumerable;
                if (instances == null)
                    return null;

                var instanceId = context.WorkflowInstanceId.ToString();
                foreach (var instance in instances)
                {
                    if (instance == null)
                        continue;

                    var instanceIdProperty = instance.GetType().GetProperty("InstanceId");
                    var currentId = instanceIdProperty?.GetValue(instance) as string;
                    if (!string.Equals(currentId, instanceId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (var variablesPropertyName in new[] { "Variables", "VariableValues" })
                    {
                        var variablesProperty = instance.GetType().GetProperty(variablesPropertyName);
                        var variables = variablesProperty?.GetValue(instance);
                        var fromDictionary = GetFromDictionary(variables, variableName);
                        if (fromDictionary != null)
                            return fromDictionary;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static object GetFromDictionary(object source, string variableName)
        {
            if (source is System.Collections.IDictionary dictionary)
            {
                foreach (System.Collections.DictionaryEntry entry in dictionary)
                {
                    if (entry.Key == null)
                        continue;

                    if (string.Equals(Convert.ToString(entry.Key), variableName, StringComparison.OrdinalIgnoreCase))
                        return entry.Value;
                }
            }

            return null;
        }
    }
}
