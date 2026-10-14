using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace F2B.OpenRpa.Design
{
    /// <summary>
    /// Bridges WF selector expressions and editor display text.
    /// Editor uses <c>{{variable}}</c> placeholders; expressions keep variables as variables
    /// (String.Format / concatenation / bare references), never as forced string literals of the name.
    /// </summary>
    internal static class SelectorExpressionBridge
    {
        private static readonly Regex VariableTokenRegex = new Regex(
            @"^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\s*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex PlaceholderRegex = new Regex(
            @"\{\{(?<name>[^{}]+)\}\}",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex FormatCallRegex = new Regex(
            @"^\s*(?:System\.)?(?:String|string)\.Format\s*\(\s*(?<rest>[\s\S]*)\s*\)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex IndexedPlaceholderRegex = new Regex(
            @"\{(?<index>\d+)(?::[^}]*)?\}",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static bool ContainsVariablePlaceholder(string displayText)
        {
            return !string.IsNullOrEmpty(displayText) && PlaceholderRegex.IsMatch(displayText);
        }

        public static bool IsWholeVariablePlaceholder(string displayText, out string variableName)
        {
            variableName = null;
            if (string.IsNullOrWhiteSpace(displayText))
            {
                return false;
            }

            var match = Regex.Match(
                displayText.Trim(),
                @"^\{\{(?<name>[^{}]+)\}\}$",
                RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            variableName = match.Groups["name"].Value.Trim();
            return !string.IsNullOrEmpty(variableName);
        }

        /// <summary>
        /// Convert a single selector expression (or literal value) into editor display text.
        /// </summary>
        public static string ExpressionToDisplay(string expressionOrLiteral, bool isLiteralValue)
        {
            if (string.IsNullOrWhiteSpace(expressionOrLiteral))
            {
                return string.Empty;
            }

            if (isLiteralValue)
            {
                return NormalizeNewlines(expressionOrLiteral);
            }

            var trimmed = expressionOrLiteral.Trim();

            if (TryParseQuotedString(trimmed, out var quoted))
            {
                return NormalizeNewlines(quoted);
            }

            if (TryParseVariableReference(trimmed, out var variableName))
            {
                return "{{" + variableName + "}}";
            }

            if (TryParseStringFormat(trimmed, out var formatDisplay))
            {
                return formatDisplay;
            }

            if (TryParseConcatenation(trimmed, out var concatDisplay))
            {
                return concatDisplay;
            }

            // Unrecognized expression: keep raw text so the user can still edit carefully.
            return trimmed;
        }

        /// <summary>
        /// Convert editor display text back to a VB expression fragment (or decide literal).
        /// </summary>
        public static bool TryDisplayToExpression(
            string displayText,
            out string expressionText,
            out bool isPureLiteral,
            out string literalValue)
        {
            expressionText = null;
            isPureLiteral = false;
            literalValue = null;

            var text = NormalizeNewlines(displayText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
            {
                isPureLiteral = true;
                literalValue = string.Empty;
                expressionText = "\"\"";
                return true;
            }

            if (IsWholeVariablePlaceholder(text, out var wholeVar))
            {
                expressionText = wholeVar;
                isPureLiteral = false;
                return true;
            }

            if (!ContainsVariablePlaceholder(text))
            {
                isPureLiteral = true;
                literalValue = text;
                expressionText = ToVbStringLiteral(text);
                return true;
            }

            var args = new List<string>();
            var formatWithIndexes = PlaceholderRegex.Replace(text, match =>
            {
                var name = match.Groups["name"].Value.Trim();
                var index = args.Count;
                args.Add(name);
                return "{" + index.ToString(CultureInfo.InvariantCulture) + "}";
            });

            var format = EscapeNonIndexBraces(formatWithIndexes);
            var argList = string.Join(", ", args);
            expressionText = "String.Format(" + ToVbStringLiteral(format) + ", " + argList + ")";
            isPureLiteral = false;
            return true;
        }

        public static string[] ExpressionsToDisplay(IEnumerable<string> items, bool treatAsLiterals)
        {
            if (items == null)
            {
                return Array.Empty<string>();
            }

            return items
                .Select(item => ExpressionToDisplay(item ?? string.Empty, treatAsLiterals))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        /// <summary>
        /// Parse a VB <c>New String() { ... }</c> expression into per-element expression fragments
        /// (preserving bare variable names and String.Format calls).
        /// </summary>
        public static string[] ParseArrayElementExpressions(string expressionText)
        {
            if (string.IsNullOrWhiteSpace(expressionText))
            {
                return Array.Empty<string>();
            }

            var trimmed = expressionText.Trim();
            var openBrace = trimmed.IndexOf('{');
            var closeBrace = trimmed.LastIndexOf('}');
            if (openBrace < 0 || closeBrace <= openBrace)
            {
                // Entire array property is a single variable / expression.
                return new[] { trimmed };
            }

            var body = trimmed.Substring(openBrace + 1, closeBrace - openBrace - 1);
            var elements = SplitTopLevelCommaSeparated(body);
            var result = new List<string>(elements.Count);
            foreach (var element in elements)
            {
                var item = element?.Trim();
                if (string.IsNullOrWhiteSpace(item))
                {
                    continue;
                }

                result.Add(item);
            }

            return result.ToArray();
        }

        public static string BuildArrayExpression(IReadOnlyList<string> displayItems)
        {
            if (displayItems == null || displayItems.Count == 0)
            {
                return "New String() {}";
            }

            var sb = new StringBuilder();
            sb.AppendLine("New String() {");
            for (var i = 0; i < displayItems.Count; i++)
            {
                if (!TryDisplayToExpression(displayItems[i], out var expr, out _, out _))
                {
                    expr = ToVbStringLiteral(displayItems[i] ?? string.Empty);
                }

                sb.Append("    ");
                sb.Append(expr);
                if (i < displayItems.Count - 1)
                {
                    sb.Append(',');
                }

                sb.AppendLine();
            }

            sb.Append('}');
            return sb.ToString();
        }

        public static bool TryBuildLiteralArray(
            IReadOnlyList<string> displayItems,
            out string[] literals)
        {
            literals = null;
            if (displayItems == null)
            {
                literals = Array.Empty<string>();
                return true;
            }

            var list = new List<string>(displayItems.Count);
            foreach (var item in displayItems)
            {
                if (!TryDisplayToExpression(item, out _, out var isPureLiteral, out var literal)
                    || !isPureLiteral)
                {
                    literals = null;
                    return false;
                }

                list.Add(literal ?? string.Empty);
            }

            literals = list.ToArray();
            return true;
        }

        private static bool TryParseVariableReference(string expression, out string name)
        {
            name = null;
            var match = VariableTokenRegex.Match(expression ?? string.Empty);
            if (!match.Success)
            {
                return false;
            }

            name = match.Groups["name"].Value;
            // Avoid treating keywords / common type names alone as variables when quoted paths fail.
            if (string.Equals(name, "Nothing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "null", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "True", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "False", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static bool TryParseStringFormat(string expression, out string display)
        {
            display = null;
            var match = FormatCallRegex.Match(expression ?? string.Empty);
            if (!match.Success)
            {
                return false;
            }

            var args = SplitTopLevelCommaSeparated(match.Groups["rest"].Value);
            if (args.Count == 0)
            {
                return false;
            }

            if (!TryParseQuotedString(args[0].Trim(), out var format))
            {
                return false;
            }

            var values = new List<string>();
            for (var i = 1; i < args.Count; i++)
            {
                var arg = args[i].Trim();
                if (TryParseQuotedString(arg, out var literalArg))
                {
                    values.Add(literalArg);
                    continue;
                }

                if (TryParseVariableReference(arg, out var varName))
                {
                    values.Add("{{" + varName + "}}");
                    continue;
                }

                // Complex arg: keep as {{arg}} using raw expression text (best-effort).
                values.Add("{{" + arg + "}}");
            }

            display = IndexedPlaceholderRegex.Replace(format, m =>
            {
                if (!int.TryParse(m.Groups["index"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                    || index < 0
                    || index >= values.Count)
                {
                    return m.Value;
                }

                return values[index];
            });

            display = NormalizeNewlines(display);
            return true;
        }

        private static bool TryParseConcatenation(string expression, out string display)
        {
            display = null;
            if (string.IsNullOrWhiteSpace(expression) || expression.IndexOf('&') < 0)
            {
                return false;
            }

            var parts = SplitTopLevelConcat(expression);
            if (parts.Count < 2)
            {
                return false;
            }

            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                var token = part.Trim();
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                if (string.Equals(token, "vbCrLf", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(token, "vbLf", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(token, "vbCr", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(token, "Environment.NewLine", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine();
                    continue;
                }

                if (TryParseQuotedString(token, out var literal))
                {
                    sb.Append(literal);
                    continue;
                }

                if (TryParseVariableReference(token, out var varName))
                {
                    sb.Append("{{");
                    sb.Append(varName);
                    sb.Append("}}");
                    continue;
                }

                return false;
            }

            display = NormalizeNewlines(sb.ToString());
            return true;
        }

        private static bool TryParseQuotedString(string token, out string value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            token = token.Trim();
            if (token.Length < 2 || token[0] != '"' || token[token.Length - 1] != '"')
            {
                return false;
            }

            var sb = new StringBuilder(token.Length);
            for (var i = 1; i < token.Length - 1; i++)
            {
                var ch = token[i];
                if (ch == '"' && i + 1 < token.Length - 1 && token[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                    continue;
                }

                sb.Append(ch);
            }

            value = sb.ToString();
            return true;
        }

        private static string ToVbStringLiteral(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            var normalized = NormalizeNewlines(value);
            var lines = normalized.Split('\n');
            if (lines.Length == 1)
            {
                return "\"" + lines[0].Replace("\"", "\"\"") + "\"";
            }

            var parts = new string[lines.Length];
            for (var i = 0; i < lines.Length; i++)
            {
                parts[i] = "\"" + lines[i].Replace("\"", "\"\"") + "\"";
            }

            return string.Join(" & vbCrLf & ", parts);
        }

        private static string EscapeNonIndexBraces(string format)
        {
            if (string.IsNullOrEmpty(format))
            {
                return string.Empty;
            }

            var tokens = new List<string>();
            var masked = IndexedPlaceholderRegex.Replace(format, match =>
            {
                var key = "\u0001" + tokens.Count.ToString(CultureInfo.InvariantCulture) + "\u0001";
                tokens.Add(match.Value);
                return key;
            });

            masked = masked.Replace("{", "{{").Replace("}", "}}");
            for (var i = 0; i < tokens.Count; i++)
            {
                masked = masked.Replace(
                    "\u0001" + i.ToString(CultureInfo.InvariantCulture) + "\u0001",
                    tokens[i]);
            }

            return masked;
        }

        private static string NormalizeNewlines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static IList<string> SplitTopLevelCommaSeparated(string body)
        {
            var items = new List<string>();
            if (string.IsNullOrWhiteSpace(body))
            {
                return items;
            }

            var start = 0;
            var depth = 0;
            var inString = false;
            for (var i = 0; i < body.Length; i++)
            {
                var ch = body[i];
                if (ch == '"')
                {
                    if (inString && i + 1 < body.Length && body[i + 1] == '"')
                    {
                        i++;
                        continue;
                    }

                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (ch == '(' || ch == '{' || ch == '[')
                {
                    depth++;
                    continue;
                }

                if (ch == ')' || ch == '}' || ch == ']')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }

                    continue;
                }

                if (ch == ',' && depth == 0)
                {
                    items.Add(body.Substring(start, i - start));
                    start = i + 1;
                }
            }

            if (start <= body.Length)
            {
                items.Add(body.Substring(start));
            }

            return items;
        }

        private static IList<string> SplitTopLevelConcat(string expression)
        {
            var items = new List<string>();
            var start = 0;
            var depth = 0;
            var inString = false;
            for (var i = 0; i < expression.Length; i++)
            {
                var ch = expression[i];
                if (ch == '"')
                {
                    if (inString && i + 1 < expression.Length && expression[i + 1] == '"')
                    {
                        i++;
                        continue;
                    }

                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (ch == '(' || ch == '{' || ch == '[')
                {
                    depth++;
                    continue;
                }

                if (ch == ')' || ch == '}' || ch == ']')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }

                    continue;
                }

                if (ch == '&' && depth == 0)
                {
                    items.Add(expression.Substring(start, i - start));
                    start = i + 1;
                }
            }

            if (start <= expression.Length)
            {
                items.Add(expression.Substring(start));
            }

            return items;
        }
    }
}
