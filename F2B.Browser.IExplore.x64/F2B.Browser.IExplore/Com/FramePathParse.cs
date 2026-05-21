using System;
using System.Collections.Generic;
using F2B.Browser.IExplore;

namespace F2B.Browser.IExplore.Com
{
    internal sealed class ParsedFrameSegment
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public int? Index { get; set; }
        public string SrcContains { get; set; }
    }

    internal static class FramePathParse
    {
        private static readonly HashSet<string> KnownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            FrameLocatorKeys.Name,
            FrameLocatorKeys.Id,
            FrameLocatorKeys.Index,
            FrameLocatorKeys.Src
        };

        public static IList<ParsedFrameSegment> Parse(IList<IDictionary<string, object>> framePath)
        {
            if (framePath == null || framePath.Count == 0)
                return new ParsedFrameSegment[0];

            var segments = new List<ParsedFrameSegment>(framePath.Count);
            for (int i = 0; i < framePath.Count; i++)
            {
                var segment = framePath[i];
                if (segment == null || segment.Count == 0)
                    throw new ArgumentException("Frame path segment " + i + " is empty.", nameof(framePath));

                var parsed = new ParsedFrameSegment();
                int setCount = 0;

                foreach (var kv in segment)
                {
                    if (string.IsNullOrWhiteSpace(kv.Key))
                        continue;

                    var key = kv.Key.Trim();
                    if (!KnownKeys.Contains(key))
                        throw new ArgumentException("Unknown frame locator key \"" + key + "\" at segment " + i + ".", nameof(framePath));

                    if (key.Equals(FrameLocatorKeys.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        parsed.Name = ToString(kv.Value);
                        if (!string.IsNullOrEmpty(parsed.Name))
                            setCount++;
                        continue;
                    }

                    if (key.Equals(FrameLocatorKeys.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        parsed.Id = ToString(kv.Value);
                        if (!string.IsNullOrEmpty(parsed.Id))
                            setCount++;
                        continue;
                    }

                    if (key.Equals(FrameLocatorKeys.Index, StringComparison.OrdinalIgnoreCase))
                    {
                        parsed.Index = ParseIndex(kv.Value, i);
                        setCount++;
                        continue;
                    }

                    if (key.Equals(FrameLocatorKeys.Src, StringComparison.OrdinalIgnoreCase))
                    {
                        parsed.SrcContains = ToString(kv.Value);
                        if (!string.IsNullOrEmpty(parsed.SrcContains))
                            setCount++;
                    }
                }

                if (setCount != 1)
                {
                    throw new ArgumentException(
                        "Frame path segment " + i + " must specify exactly one of: "
                        + FrameLocatorKeys.Name + ", "
                        + FrameLocatorKeys.Id + ", "
                        + FrameLocatorKeys.Index + ", "
                        + FrameLocatorKeys.Src + ".",
                        nameof(framePath));
                }

                segments.Add(parsed);
            }

            return segments;
        }

        private static int ParseIndex(object raw, int segmentIndex)
        {
            if (raw is int i)
                return i;
            if (raw is long l)
                return (int)l;
            if (int.TryParse(raw?.ToString(), out var parsed))
                return parsed;

            throw new ArgumentException("Invalid index at frame path segment " + segmentIndex + ": " + raw);
        }

        private static string ToString(object raw) => raw == null ? string.Empty : raw.ToString();
    }
}
