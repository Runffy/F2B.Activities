using System;
using System.Activities;
using System.Activities.Expressions;
using System.ComponentModel;
using System.Reflection;

namespace F2B.Browser.Chromium.Playwright
{
    internal static class ActivityArgumentHelper
    {
        public static T GetOrDefault<T>(InArgument<T> argument, CodeActivityContext context, T fallback)
        {
            if (argument == null || argument.Expression == null)
            {
                return fallback;
            }

            return argument.Get(context);
        }

        public static bool HasExpression(Argument argument)
        {
            return argument != null && argument.Expression != null;
        }

        public static bool HasExpression<T>(InArgument<T> argument)
        {
            return HasExpression((Argument)argument);
        }

        public static PwElement GetPwElement(InArgument<PwElement> argument, CodeActivityContext context)
        {
            if (!HasExpression(argument))
            {
                return null;
            }

            try
            {
                var direct = argument.Get(context);
                if (direct != null)
                {
                    return direct;
                }
            }
            catch
            {
                // Fall back only when the bound expression cannot be evaluated normally.
            }

            var variableName = ExtractVariableName(argument.Expression);
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return null;
            }

            var resolved = ResolveWorkflowVariable(context, variableName);
            return AsPwElement(resolved);
        }

        public static void SetPwElement(OutArgument<PwElement> argument, ActivityContext context, PwElement value)
        {
            if (argument == null || !HasExpression(argument))
            {
                return;
            }

            var variableName = ExtractVariableName(argument.Expression);
            Exception setException = null;

            try
            {
                argument.Set(context, value);
                return;
            }
            catch (Exception ex)
            {
                setException = ex;
            }

            if (!string.IsNullOrWhiteSpace(variableName))
            {
                if (TrySetWorkflowVariable(context, variableName, value))
                {
                    return;
                }
            }

            var displayName = TryGetBoundVariableName(argument);
            throw new InvalidOperationException(
                "Failed to assign PwElement to '" + displayName + "'. " +
                (setException == null
                    ? "Verify the target variable exists in the same scope and is writable."
                    : setException.Message),
                setException);
        }

        public static PwElement AsPwElement(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is PwElement pwElement)
            {
                return pwElement;
            }

            throw new ArgumentException($"Expected {typeof(PwElement).FullName}, but received {value.GetType().FullName}.");
        }

        public static string TryGetBoundVariableName(Argument argument)
        {
            if (argument == null || argument.Expression == null)
            {
                return string.Empty;
            }

            return ExtractVariableName(argument.Expression) ?? argument.Expression.ToString();
        }

        public static string ToWaitStateString(FindElementWaitState waitState)
        {
            switch (waitState)
            {
                case FindElementWaitState.None:
                    return null;
                case FindElementWaitState.Visible:
                    return "visible";
                case FindElementWaitState.Attached:
                    return "attached";
                case FindElementWaitState.Hidden:
                    return "hidden";
                case FindElementWaitState.Detached:
                    return "detached";
                default:
                    throw new ArgumentOutOfRangeException(nameof(waitState), waitState, "Unsupported WaitState.");
            }
        }

        private static string ExtractVariableName(Activity expression)
        {
            if (expression == null)
            {
                return null;
            }

            var textExpression = expression as ITextExpression;
            if (textExpression != null)
            {
                var fromText = NormalizeVariableName(textExpression.ExpressionText);
                if (!string.IsNullOrWhiteSpace(fromText))
                {
                    return fromText;
                }
            }

            var expressionType = expression.GetType();
            foreach (var propertyName in new[] { "VariableName", "ExpressionText", "Name" })
            {
                var property = expressionType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null || property.PropertyType != typeof(string))
                {
                    continue;
                }

                var fromProperty = NormalizeVariableName(property.GetValue(expression) as string);
                if (!string.IsNullOrWhiteSpace(fromProperty))
                {
                    return fromProperty;
                }
            }

            if (expressionType.IsGenericType &&
                expressionType.GetGenericTypeDefinition().FullName != null &&
                expressionType.GetGenericTypeDefinition().FullName.StartsWith("System.Activities.Expressions.Literal", StringComparison.Ordinal))
            {
                var valueProperty = expressionType.GetProperty("Value");
                var literalValue = valueProperty?.GetValue(expression);
                if (literalValue is string literalText)
                {
                    return NormalizeVariableName(literalText);
                }
            }

            var locationReference = TryGetLocationReference(expression);
            if (!string.IsNullOrWhiteSpace(locationReference))
            {
                return NormalizeVariableName(locationReference);
            }

            return NormalizeVariableName(expression.ToString());
        }

        private static string TryGetLocationReference(Activity expression)
        {
            var expressionType = expression.GetType();
            foreach (var propertyName in new[] { "LocationReference", "Reference", "Variable" })
            {
                var property = expressionType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    continue;
                }

                var reference = property.GetValue(expression);
                if (reference == null)
                {
                    continue;
                }

                var nameProperty = reference.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                var name = nameProperty?.GetValue(reference) as string;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }

            return null;
        }

        private static string NormalizeVariableName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            text = text.Trim();

            if (text.StartsWith("[", StringComparison.Ordinal) && text.EndsWith("]", StringComparison.Ordinal))
            {
                text = text.Substring(1, text.Length - 2).Trim();
            }

            if (text.Length >= 2 && text.StartsWith("\"", StringComparison.Ordinal) && text.EndsWith("\"", StringComparison.Ordinal))
            {
                text = text.Substring(1, text.Length - 2).Trim();
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // Ignore complex expressions; only treat simple identifiers as variable names.
            if (text.IndexOfAny(new[] { ' ', '.', '(', ')', '+', '-', '*', '/', '&', '|', '=', '<', '>' }) >= 0)
            {
                return null;
            }

            return text;
        }

        private static object ResolveWorkflowVariable(ActivityContext context, string variableName)
        {
            variableName = NormalizeVariableName(variableName);
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return null;
            }

            var fromDataContext = GetFromDataContext(context, variableName);
            if (fromDataContext != null)
            {
                return fromDataContext;
            }

            return TryGetOpenRpaVariable(context, variableName);
        }

        private static object GetFromDataContext(ActivityContext context, string variableName)
        {
            var dataContext = context.DataContext;
            if (dataContext == null)
            {
                return null;
            }

            var dataType = dataContext.GetType();
            var property = dataType.GetProperty(
                variableName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property != null)
            {
                return property.GetValue(dataContext);
            }

            foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(dataContext))
            {
                if (string.Equals(descriptor.Name, variableName, StringComparison.OrdinalIgnoreCase))
                {
                    return descriptor.GetValue(dataContext);
                }
            }

            return null;
        }

        private static bool TrySetWorkflowVariable(ActivityContext context, string variableName, object value)
        {
            variableName = NormalizeVariableName(variableName);
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return false;
            }

            if (TrySetDataContextVariable(context, variableName, value))
            {
                return true;
            }

            return TrySetOpenRpaVariable(context, variableName, value);
        }

        private static bool TrySetDataContextVariable(ActivityContext context, string variableName, object value)
        {
            var dataContext = context.DataContext;
            if (dataContext == null)
            {
                return false;
            }

            var dataType = dataContext.GetType();
            var property = dataType.GetProperty(
                variableName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property != null && property.CanWrite)
            {
                property.SetValue(dataContext, value);
                return true;
            }

            foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(dataContext))
            {
                if (!descriptor.IsReadOnly &&
                    string.Equals(descriptor.Name, variableName, StringComparison.OrdinalIgnoreCase))
                {
                    descriptor.SetValue(dataContext, value);
                    return true;
                }
            }

            return false;
        }

        private static bool TrySetOpenRpaVariable(ActivityContext context, string variableName, object value)
        {
            try
            {
                var workflowInstanceType = Type.GetType("OpenRPA.WorkflowInstance, OpenRPA", false);
                if (workflowInstanceType == null)
                {
                    return false;
                }

                var instancesProperty = workflowInstanceType.GetProperty("Instances", BindingFlags.Public | BindingFlags.Static);
                var instances = instancesProperty?.GetValue(null) as System.Collections.IEnumerable;
                if (instances == null)
                {
                    return false;
                }

                var instanceId = context.WorkflowInstanceId.ToString();
                foreach (var instance in instances)
                {
                    if (instance == null)
                    {
                        continue;
                    }

                    var instanceIdProperty = instance.GetType().GetProperty("InstanceId");
                    var currentId = instanceIdProperty?.GetValue(instance) as string;
                    if (!string.Equals(currentId, instanceId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (var variablesPropertyName in new[] { "Variables", "VariableValues" })
                    {
                        var variablesProperty = instance.GetType().GetProperty(variablesPropertyName);
                        var variables = variablesProperty?.GetValue(instance);
                        if (TrySetDictionaryValue(variables, variableName, value))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TrySetDictionaryValue(object source, string variableName, object value)
        {
            if (source == null)
            {
                return false;
            }

            if (source is System.Collections.IDictionary dictionary && dictionary.IsFixedSize == false)
            {
                dictionary[variableName] = value;
                return true;
            }

            return false;
        }

        private static object TryGetOpenRpaVariable(ActivityContext context, string variableName)
        {
            try
            {
                var workflowInstanceType = Type.GetType("OpenRPA.WorkflowInstance, OpenRPA", false);
                if (workflowInstanceType == null)
                {
                    return null;
                }

                var instancesProperty = workflowInstanceType.GetProperty("Instances", BindingFlags.Public | BindingFlags.Static);
                var instances = instancesProperty?.GetValue(null) as System.Collections.IEnumerable;
                if (instances == null)
                {
                    return null;
                }

                var instanceId = context.WorkflowInstanceId.ToString();
                foreach (var instance in instances)
                {
                    if (instance == null)
                    {
                        continue;
                    }

                    var instanceIdProperty = instance.GetType().GetProperty("InstanceId");
                    var currentId = instanceIdProperty?.GetValue(instance) as string;
                    if (!string.Equals(currentId, instanceId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (var variablesPropertyName in new[] { "Variables", "VariableValues" })
                    {
                        var variablesProperty = instance.GetType().GetProperty(variablesPropertyName);
                        var variables = variablesProperty?.GetValue(instance);
                        var fromDictionary = GetFromNameValueCollection(variables, variableName);
                        if (fromDictionary != null)
                        {
                            return fromDictionary;
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static object GetFromNameValueCollection(object source, string variableName)
        {
            if (source == null)
            {
                return null;
            }

            if (source is System.Collections.IDictionary dictionary)
            {
                foreach (System.Collections.DictionaryEntry entry in dictionary)
                {
                    if (entry.Key == null)
                    {
                        continue;
                    }

                    if (string.Equals(Convert.ToString(entry.Key), variableName, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Value;
                    }
                }
            }

            return null;
        }
    }
}
