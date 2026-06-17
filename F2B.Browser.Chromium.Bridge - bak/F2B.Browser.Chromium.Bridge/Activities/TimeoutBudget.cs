using System;
using System.Diagnostics;

namespace F2B.Browser.Chromium.Bridge
{
    internal sealed class TimeoutBudget
    {
        private readonly int _totalMs;
        private readonly Stopwatch _stopwatch;

        public TimeoutBudget(int totalMs)
        {
            _totalMs = Math.Max(0, totalMs);
            _stopwatch = Stopwatch.StartNew();
        }

        public int RemainingMs
        {
            get
            {
                var remaining = _totalMs - (int)_stopwatch.ElapsedMilliseconds;
                return remaining > 0 ? remaining : 0;
            }
        }
    }
}
