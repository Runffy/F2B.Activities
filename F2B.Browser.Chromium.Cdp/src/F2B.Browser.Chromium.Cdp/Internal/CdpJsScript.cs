using System;
using System.Text.RegularExpressions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    /// <summary>
    /// Helpers for preparing user-supplied JavaScript for CDP Runtime.callFunctionOn.
    /// </summary>
    internal static class CdpJsScript
    {
        private static readonly Regex ReturnKeyword = new Regex(
            @"\breturn\b",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Wraps a script body as a function declaration. Bare expressions such as
        /// <c>1 + 2</c> are auto-returned so callers get a value without writing <c>return</c>.
        /// </summary>
        public static string WrapAsFunction(string script)
        {
            if (IsJsFunction(script))
            {
                return script;
            }

            if (ShouldAutoReturnExpression(script))
            {
                var expression = script.Trim().TrimEnd(';').Trim();
                return "function() {\nreturn (" + expression + ");\n}";
            }

            return "function() {\n" + script + "\n}";
        }

        public static bool IsJsFunction(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                return false;
            }

            var trimmed = script.TrimStart();
            if (trimmed.StartsWith("function", StringComparison.Ordinal) ||
                trimmed.StartsWith("async function", StringComparison.Ordinal))
            {
                return true;
            }

            return trimmed.StartsWith("(", StringComparison.Ordinal) &&
                   trimmed.IndexOf("=>", StringComparison.Ordinal) >= 0;
        }

        private static bool ShouldAutoReturnExpression(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                return false;
            }

            var trimmed = script.Trim();
            if (ReturnKeyword.IsMatch(trimmed))
            {
                return false;
            }

            if (trimmed.IndexOf('\n') >= 0 || trimmed.IndexOf('\r') >= 0)
            {
                return false;
            }

            var withoutTrailingSemi = trimmed.TrimEnd(';').Trim();
            if (withoutTrailingSemi.IndexOf(';') >= 0)
            {
                return false;
            }

            return !StartsWithStatementKeyword(withoutTrailingSemi);
        }

        private static bool StartsWithStatementKeyword(string script)
        {
            return StartsWithWord(script, "var")
                || StartsWithWord(script, "let")
                || StartsWithWord(script, "const")
                || StartsWithWord(script, "if")
                || StartsWithWord(script, "for")
                || StartsWithWord(script, "while")
                || StartsWithWord(script, "do")
                || StartsWithWord(script, "switch")
                || StartsWithWord(script, "try")
                || StartsWithWord(script, "throw")
                || StartsWithWord(script, "class")
                || StartsWithWord(script, "function")
                || StartsWithWord(script, "async");
        }

        private static bool StartsWithWord(string script, string word)
        {
            if (!script.StartsWith(word, StringComparison.Ordinal))
            {
                return false;
            }

            if (script.Length == word.Length)
            {
                return true;
            }

            var next = script[word.Length];
            return !char.IsLetterOrDigit(next) && next != '_' && next != '$';
        }
    }
}
