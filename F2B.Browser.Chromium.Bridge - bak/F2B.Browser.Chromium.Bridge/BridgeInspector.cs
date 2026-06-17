using System;
using System.Collections.Generic;
using System.Linq;
using F2B.Browser.Chromium.Bridge.Selectors;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeInspectorPickResult
    {
        public object[] Segments { get; set; }

        public IList<SelectorLevel> Levels { get; set; }

        public string DisplayName { get; set; }

        public string TabTitle { get; set; }

        public string TabUrl { get; set; }

        public bool Cancelled { get; set; }

        public bool RestrictedUrl { get; set; }

        /// <summary>
        /// When Cancelled is true, non-empty values such as tab-changed / page-navigated / tab-closed.
        /// </summary>
        public string InvalidatedReason { get; set; }
    }

    public sealed class BridgeInspectorHoverResult
    {
        public bool Hovered { get; set; }

        public System.Drawing.Rectangle? Bounds { get; set; }
    }

    public sealed class BridgeInspectorBuildResult
    {
        public IList<SelectorLevel> Levels { get; set; }

        public object[] Segments { get; set; }

        public string DisplayName { get; set; }
    }

    public sealed class BridgeInspectorDescribeResult
    {
        public IList<BridgeInspectorProperty> Properties { get; set; }

        public string DisplayName { get; set; }
    }

    public sealed class BridgeInspectorProperty
    {
        public string Name { get; set; }

        public string Value { get; set; }
    }

    public sealed class BridgeInspectorDomNode
    {
        public string DisplayName { get; set; }

        public object[] Segments { get; set; }

        public object[] FrameSegments { get; set; }

        public int ChildCount { get; set; }

        public bool HasFrameChildren { get; set; }

        public string TagName { get; set; }
    }

    public sealed class BridgeInspectorValidateProbeResult
    {
        public string DocumentTitle { get; set; }

        public string LocationHref { get; set; }

        public bool SegmentResolved { get; set; }

        public string SegmentElementId { get; set; }

        public string SegmentElementTag { get; set; }

        public int SelectorMatchCount { get; set; }

        public string SelectorError { get; set; }

        public string FrameError { get; set; }

        public bool GetElementByIdFound { get; set; }

        public string GetElementByIdTag { get; set; }

        public string ProbeId { get; set; }
    }

    public static class BridgeInspectorParser
    {
        public static IList<SelectorLevel> ParseLevels(object[] rawLevels)
        {
            var levels = new List<SelectorLevel>();
            if (rawLevels == null)
                return levels;

            foreach (var rawLevel in rawLevels.OfType<Dictionary<string, object>>())
            {
                var tagName = BridgeJson.GetString(rawLevel, "tagName") ?? "ctrl";
                var level = new SelectorLevel(tagName)
                {
                    IsEnabled = BridgeJson.GetBool(rawLevel, "isEnabled", true),
                    CanDisable = BridgeJson.GetBool(rawLevel, "canDisable", true)
                };

                foreach (var rawProperty in BridgeJson.GetArray(rawLevel, "properties").OfType<Dictionary<string, object>>())
                {
                    level.Properties.Add(new SelectorProperty
                    {
                        Name = BridgeJson.GetString(rawProperty, "name"),
                        Value = BridgeJson.GetString(rawProperty, "value"),
                        IsSelected = BridgeJson.GetBool(rawProperty, "isSelected", true),
                        IsRegex = BridgeJson.GetBool(rawProperty, "isRegex")
                    });
                }

                levels.Add(level);
            }

            return levels;
        }

        public static BridgeInspectorDomNode[] ParseDomNodes(object[] rawNodes)
        {
            if (rawNodes == null)
                return new BridgeInspectorDomNode[0];

            return rawNodes
                .OfType<Dictionary<string, object>>()
                .Select(item => new BridgeInspectorDomNode
                {
                    DisplayName = BridgeJson.GetString(item, "displayName"),
                    Segments = ToSegmentArray(BridgeJson.GetArray(item, "segments")),
                    FrameSegments = ToSegmentArray(BridgeJson.GetArray(item, "frameSegments")),
                    ChildCount = BridgeJson.GetInt(item, "childCount"),
                    HasFrameChildren = BridgeJson.GetBool(item, "hasFrameChildren"),
                    TagName = BridgeJson.GetString(item, "tagName")
                })
                .ToArray();
        }

        public static IList<BridgeInspectorProperty> ParseProperties(object[] rawProperties)
        {
            if (rawProperties == null)
                return new List<BridgeInspectorProperty>();

            return rawProperties
                .OfType<Dictionary<string, object>>()
                .Select(item => new BridgeInspectorProperty
                {
                    Name = BridgeJson.GetString(item, "name"),
                    Value = BridgeJson.GetString(item, "value")
                })
                .ToList();
        }

        public static BridgeInspectorValidateProbeResult ParseValidateProbe(Dictionary<string, object> data)
        {
            if (data == null)
                return new BridgeInspectorValidateProbeResult();

            return new BridgeInspectorValidateProbeResult
            {
                DocumentTitle = BridgeJson.GetString(data, "documentTitle"),
                LocationHref = BridgeJson.GetString(data, "locationHref"),
                SegmentResolved = BridgeJson.GetBool(data, "segmentResolved"),
                SegmentElementId = BridgeJson.GetString(data, "segmentElementId"),
                SegmentElementTag = BridgeJson.GetString(data, "segmentElementTag"),
                SelectorMatchCount = BridgeJson.GetInt(data, "selectorMatchCount"),
                SelectorError = BridgeJson.GetString(data, "selectorError"),
                FrameError = BridgeJson.GetString(data, "frameError"),
                GetElementByIdFound = BridgeJson.GetBool(data, "getElementByIdFound"),
                GetElementByIdTag = BridgeJson.GetString(data, "getElementByIdTag"),
                ProbeId = BridgeJson.GetString(data, "probeId")
            };
        }

        private static object[] ToSegmentArray(object[] rawSegments)
        {
            if (rawSegments == null || rawSegments.Length == 0)
                return null;

            return rawSegments;
        }
    }
}
