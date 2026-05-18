using System;
using System.Diagnostics;

namespace F2B.Browser.Chromium.Playwright
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

        public int TotalMs => _totalMs;

        public int RemainingMs
        {
            get
            {
                var remaining = _totalMs - (int)_stopwatch.ElapsedMilliseconds;
                return remaining > 0 ? remaining : 0;
            }
        }

        public double? RemainingAsNullableDouble()
        {
            return RemainingMs;
        }
    }
}
