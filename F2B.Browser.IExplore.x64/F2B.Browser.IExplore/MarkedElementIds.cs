using System;
using System.Globalization;

namespace F2B.Browser.IExplore
{
    /// <summary>
    /// Temporary DOM <c>id</c> for weak locators: has native id → use it; otherwise mark with <see cref="Generate"/> then reuse.
    /// </summary>
    public static class MarkedElementIds
    {
        /// <summary>Prefix for RPA-injected ids. Full id = prefix + Guid (32 hex chars).</summary>
        public const string IdPrefix = ElementLocatorKeys.Prefix + "mark.";

        /// <summary>New mark id: <see cref="IdPrefix"/> + UUID (collision risk negligible).</summary>
        public static string Generate() => IdPrefix + Guid.NewGuid().ToString("N");

        /// <summary>True when <paramref name="elementId"/> was created by <see cref="Generate"/>.</summary>
        public static bool IsMarkedId(string elementId) =>
            !string.IsNullOrEmpty(elementId)
            && elementId.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>Locator JSON for a marked (or any) id.</summary>
        public static string LocatorJson(string elementId) =>
            "{'id':'" + (elementId ?? string.Empty).Replace("'", "\\'") + "'}";

        /// <summary>
        /// JavaScript: <paramref name="findScript"/> must evaluate to one element; sets its <c>id</c> to <paramref name="markId"/>.
        /// </summary>
        public static string BuildAssignIdScript(string findScript, string markId)
        {
            if (string.IsNullOrWhiteSpace(findScript))
                throw new ArgumentException("findScript is required.", nameof(findScript));
            if (string.IsNullOrWhiteSpace(markId) || !IsMarkedId(markId))
                throw new ArgumentException("markId must use " + IdPrefix, nameof(markId));

            var safeId = markId.Replace("'", "\\'");
            return string.Format(
                CultureInfo.InvariantCulture,
                "(function() {{" +
                " var el = ({0});" +
                " if (!el) return false;" +
                " var id = '{1}';" +
                " var prev = document.getElementById(id);" +
                " if (prev && prev !== el) prev.removeAttribute('id');" +
                " el.id = id;" +
                " return true;" +
                "}})()",
                findScript.Trim(),
                safeId);
        }

        /// <summary>Removes a mark id from the document if present (optional cleanup).</summary>
        public static string BuildRemoveIdScript(string markId)
        {
            if (string.IsNullOrWhiteSpace(markId))
                return "false";
            var safeId = markId.Replace("'", "\\'");
            return string.Format(
                CultureInfo.InvariantCulture,
                "(function() {{ var el = document.getElementById('{0}'); if (!el) return false; el.removeAttribute('id'); return true; }})()",
                safeId);
        }
    }
}
