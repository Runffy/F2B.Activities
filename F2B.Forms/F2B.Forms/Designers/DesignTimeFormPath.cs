using System;
using System.Activities;
using System.Activities.Expressions;
using System.Activities.Presentation.Model;
using System.IO;
using System.Reflection;
using F2B.Forms.Activities;
using F2B.Forms.Model;

namespace F2B.Forms.Designers
{
    internal static class DesignTimeFormPath
    {
        public static ModelItem FindAsyncForm(ModelItem start)
        {
            ModelItem current = start;
            while (current != null)
            {
                if (current.ItemType != null && typeof(AsyncFormActivity).IsAssignableFrom(current.ItemType))
                {
                    return current;
                }

                current = current.Parent;
            }

            return null;
        }

        public static bool TryGetLiteralFormPath(ModelItem bindEventItem, out string path, out string status)
        {
            path = null;
            status = null;

            ModelItem asyncForm = FindAsyncForm(bindEventItem);
            if (asyncForm == null)
            {
                status = "Bind Event must be under Async Form.";
                return false;
            }

            ModelItem formPathArg = asyncForm.Properties["FormPath"] == null
                ? null
                : asyncForm.Properties["FormPath"].Value;

            if (!TryGetLiteralString(formPathArg, out path))
            {
                status = "Form Path is not a literal absolute path. Dropdown disabled — type Control Id / Event Name manually, or set Form Path to a quoted path literal.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                status = "Form Path is empty.";
                return false;
            }

            if (!File.Exists(path))
            {
                status = "Form JSON not found: " + path;
                return false;
            }

            status = "Loaded: " + path;
            return true;
        }

        public static bool TryLoadForm(ModelItem bindEventItem, out FormDefinition definition, out string status)
        {
            definition = null;
            if (!TryGetLiteralFormPath(bindEventItem, out string path, out status))
            {
                return false;
            }

            try
            {
                definition = Engine.FormJsonLoader.LoadFromFile(path);
                status = "Loaded: " + path;
                return true;
            }
            catch (Exception ex)
            {
                status = "Failed to load form JSON: " + ex.Message;
                return false;
            }
        }

        public static bool TryGetLiteralString(ModelItem argumentModelItem, out string value)
        {
            value = null;
            if (argumentModelItem == null)
            {
                return false;
            }

            object current = argumentModelItem.GetCurrentValue();
            if (current is InArgument<string> inArgument)
            {
                return TryGetLiteralFromArgument(inArgument, out value);
            }

            if (current is InArgument inArgNonGeneric)
            {
                Activity expression = inArgNonGeneric.Expression;
                return TryGetLiteralFromExpression(expression, out value);
            }

            return false;
        }

        public static void SetLiteralString(ModelItem owner, string propertyName, string value)
        {
            if (owner == null || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            owner.Properties[propertyName].SetValue(new InArgument<string>(value ?? string.Empty));
        }

        /// <summary>
        /// Empty / whitespace clears the argument (null = do not change at runtime for optional fields).
        /// </summary>
        public static void SetOptionalLiteralString(ModelItem owner, string propertyName, string value)
        {
            if (owner == null || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                owner.Properties[propertyName].SetValue(null);
                return;
            }

            owner.Properties[propertyName].SetValue(new InArgument<string>(value.Trim()));
        }

        private static bool TryGetLiteralFromArgument(InArgument<string> argument, out string value)
        {
            value = null;
            if (argument == null)
            {
                return false;
            }

            return TryGetLiteralFromExpression(argument.Expression, out value);
        }

        private static bool TryGetLiteralFromExpression(Activity expression, out string value)
        {
            value = null;
            if (expression == null)
            {
                return false;
            }

            if (expression is Literal<string> literal)
            {
                value = literal.Value;
                return true;
            }

            // VisualBasicValue<string> / CSharpValue<string>: ExpressionText like "C:\a.json" or "\"C:\a.json\""
            PropertyInfo textProp = expression.GetType().GetProperty("ExpressionText");
            if (textProp == null)
            {
                return false;
            }

            string text = textProp.GetValue(expression, null) as string;
            return TryParseQuotedOrBarePath(text, out value);
        }

        private static bool TryParseQuotedOrBarePath(string expressionText, out string value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(expressionText))
            {
                return false;
            }

            string text = expressionText.Trim();

            // Quoted VB/C# string literal: "Closing", "C:\path.json", etc.
            if ((text.StartsWith("\"", StringComparison.Ordinal) && text.EndsWith("\"", StringComparison.Ordinal) && text.Length >= 2)
                || (text.StartsWith("'", StringComparison.Ordinal) && text.EndsWith("'", StringComparison.Ordinal) && text.Length >= 2))
            {
                value = Unescape(text.Substring(1, text.Length - 2));
                return true;
            }

            // Bare absolute path without quotes (some designers store FormPath this way)
            if (IsAbsolutePath(text) && text.IndexOfAny(new[] { '+', '&', '(' }) < 0)
            {
                value = text;
                return true;
            }

            return false;
        }

        private static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.Replace("\"\"", "\"").Replace("\\\"", "\"");
        }

        private static bool IsAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                return Path.IsPathRooted(path);
            }
            catch
            {
                return false;
            }
        }
    }
}
