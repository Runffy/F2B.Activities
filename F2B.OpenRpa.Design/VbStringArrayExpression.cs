using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace F2B.OpenRpa.Design
{
    internal static class VbStringArrayExpression
    {
        private static readonly Regex VbNewLineConcatRegex = new Regex(
            @"\s*&\s*vb(?:CrLf|Lf|Cr)\s*&\s*",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static string Build(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return "New String() {}";
            }

            var sb = new StringBuilder();
            sb.AppendLine("New String() {");
            for (var i = 0; i < values.Count; i++)
            {
                sb.Append("    ");
                sb.Append(ToStringLiteral(values[i] ?? string.Empty));
                if (i < values.Count - 1)
                {
                    sb.Append(',');
                }

                sb.AppendLine();
            }

            sb.Append('}');
            return sb.ToString();
        }

        public static string[] Parse(string expressionText)
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
                return Array.Empty<string>();
            }

            var body = trimmed.Substring(openBrace + 1, closeBrace - openBrace - 1);
            var items = SplitArrayElements(body);
            var result = new List<string>(items.Count);
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item))
                {
                    continue;
                }

                result.Add(ParseStringExpression(item.Trim()));
            }

            return result.ToArray();
        }

        private static string ToStringLiteral(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = normalized.Split('\n');
            if (lines.Length == 1)
            {
                return "\"" + EscapeQuotes(lines[0]) + "\"";
            }

            var parts = new string[lines.Length];
            for (var i = 0; i < lines.Length; i++)
            {
                parts[i] = "\"" + EscapeQuotes(lines[i]) + "\"";
            }

            return string.Join(" & vbCrLf & ", parts);
        }

        private static string EscapeQuotes(string text)
        {
            return (text ?? string.Empty).Replace("\"", "\"\"");
        }

        private static IList<string> SplitArrayElements(string body)
        {
            var items = new List<string>();
            if (string.IsNullOrWhiteSpace(body))
            {
                return items;
            }

            var start = 0;
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

                if (ch == ',' && !inString)
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

        private static string ParseStringExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return string.Empty;
            }

            var parts = VbNewLineConcatRegex.Split(expression);
            if (parts.Length == 1)
            {
                return ParseQuotedString(parts[0].Trim());
            }

            var sb = new StringBuilder();
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(ParseQuotedString(parts[i].Trim()));
            }

            return sb.ToString();
        }

        private static string ParseQuotedString(string token)
        {
            token = token.Trim();
            if (token.Length < 2 || token[0] != '"' || token[token.Length - 1] != '"')
            {
                return token;
            }

            var inner = token.Substring(1, token.Length - 2);
            return inner.Replace("\"\"", "\"");
        }
    }
}
