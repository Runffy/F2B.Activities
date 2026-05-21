using System;

namespace F2B.Browser.IExplore
{
    /// <summary>Result of <see cref="EmbeddedIEWindow.ExecuteScript"/>.</summary>
    public sealed class IeScriptResult
    {
        internal IeScriptResult(object raw)
        {
            Raw = raw;
        }

        /// <summary>Raw value returned from <c>window.__ieExecResult</c>.</summary>
        public object Raw { get; }

        public string AsString() => Raw == null ? null : Raw.ToString();

        public bool? AsBool()
        {
            if (Raw is bool b)
                return b;
            if (bool.TryParse(AsString(), out var parsed))
                return parsed;
            return null;
        }

        public int? AsInt()
        {
            if (Raw is int i)
                return i;
            if (Raw is long l)
                return (int)l;
            if (int.TryParse(AsString(), out var parsed))
                return parsed;
            return null;
        }
    }
}
