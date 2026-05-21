using System;
using System.Collections.Generic;
using F2B.Browser.IExplore.Com;

namespace F2B.Browser.IExplore
{
    /// <summary>
    /// Element + nested frame path as JSON strings (single quotes OK in VB).
    /// <paramref name="framePath"/> <c>null</c> is treated as <c>[]</c> (root document).
    /// </summary>
    public sealed class IELocator
    {
        public IELocator(string element, string framePath = null)
        {
            if (string.IsNullOrWhiteSpace(element))
                throw new ArgumentException("Element JSON is required.", nameof(element));

            Element = element.Trim();
            FramePath = framePath;
        }

        /// <summary>Element filters / action keys (JSON object).</summary>
        public string Element { get; }

        /// <summary>Frame path (JSON array). <c>null</c> or <c>[]</c> = root document.</summary>
        public string FramePath { get; }

        /// <summary>Root document, no nested frames.</summary>
        public static IELocator Root(string elementJson) => new IELocator(elementJson, null);

        /// <summary>Frame path only (element JSON is not used by <see cref="EmbeddedIEWindow.WaitForFrame"/>).</summary>
        public static IELocator ForFrame(string framePathJson) =>
            new IELocator("{'tag':'html'}", framePathJson);

        public IDictionary<string, object> ParseElement() =>
            IEJsonParse.ParseDictionary(Element);

        public IList<IDictionary<string, object>> ParseFramePath()
        {
            if (string.IsNullOrWhiteSpace(FramePath))
                return null;

            var trimmed = FramePath.Trim();
            if (trimmed == "[]")
                return null;

            return IEJsonParse.ParseFramePath(trimmed);
        }
    }
}
