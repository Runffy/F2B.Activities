using System.Collections.Generic;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>JSON payload for <c>IExplore.ComHost.exe dom</c> (x86 MSHTML proxy).</summary>
    internal sealed class IeComHostDomRequest
    {
        public string Op { get; set; }
        public string Url { get; set; }
        public string Script { get; set; }
        public string ElementJson { get; set; }
        public string FramePathJson { get; set; }
        public string TargetElementJson { get; set; }
        /// <summary>Parent locator JSON for scoped find/operations (frame path on child is ignored).</summary>
        public string ScopeElementJson { get; set; }
        public string ScopeFramePathJson { get; set; }
        /// <summary>Zero-based index on <see cref="ScopeElementJson"/>; -1 if not used.</summary>
        public int ScopeElementIdx { get; set; } = -1;
        public string ArgsJson { get; set; }
        public string Value { get; set; }
        public string AttributeName { get; set; }
        public string LocatorsJson { get; set; }
        public int Timeout { get; set; } = 30000;
        public int MatchIndex { get; set; }
        public int ClickIntervalMs { get; set; } = 100;
        public string Button { get; set; }
        public string Mode { get; set; }
        public Dictionary<string, object> Extra { get; set; }
    }

    internal sealed class IeComHostDomResponse
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public object Result { get; set; }
        public string StringResult { get; set; }
        public bool? BoolResult { get; set; }
        public int? IntResult { get; set; }
        public bool Found { get; set; }
        public int? ParallelIndex { get; set; }
    }
}
