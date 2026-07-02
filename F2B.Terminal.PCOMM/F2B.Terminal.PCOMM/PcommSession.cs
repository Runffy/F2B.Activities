using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace F2B.Terminal.PCOMM
{
    /// <summary>
    /// Wrapper for the PCOMM autECLPS presentation space (Python: ps).
    /// </summary>
    public sealed class PcommSession
    {
        internal PcommSession(object presentationSpace, string sessionName)
        {
            PresentationSpace = presentationSpace ?? throw new ArgumentNullException(nameof(presentationSpace));
            SessionName = sessionName ?? throw new ArgumentNullException(nameof(sessionName));
        }

        internal object PresentationSpace { get; }

        public string SessionName { get; }

        public string ReadSingleRow(int row, int? startCol = null, int? endCol = null)
        {
            dynamic presentationSpace = PresentationSpace;

            var startIndex = startCol ?? 1;
            if (startIndex < 1)
            {
                startIndex = 1;
            }

            var numCols = GetNumCols(presentationSpace);

            var endIndex = endCol ?? numCols;
            if (endIndex < 1)
            {
                endIndex = numCols;
            }

            if (endIndex > numCols)
            {
                endIndex = numCols;
            }

            if (startIndex > endIndex)
            {
                startIndex = endIndex;
            }

            var length = endIndex - startIndex + 1;
            return presentationSpace.GetText(row, startIndex, length);
        }

        public string ReadAllRows()
        {
            dynamic presentationSpace = PresentationSpace;
            var numRows = GetNumRows(presentationSpace);
            var lines = new string[numRows];

            for (var row = 1; row <= numRows; row++)
            {
                lines[row - 1] = ReadSingleRow(row);
            }

            return string.Join(Environment.NewLine, lines);
        }

        public void SetCursorPos(int rowIndex, int columnIndex)
        {
            dynamic presentationSpace = PresentationSpace;
            presentationSpace.SetCursorPos(rowIndex, columnIndex);
        }

        public void InputText(int rowIndex, int columnIndex, string text)
        {
            SetCursorPos(rowIndex, columnIndex);
            dynamic presentationSpace = PresentationSpace;
            presentationSpace.SendKeys(text ?? string.Empty);
        }

        public void SendKey(PcommKey key)
        {
            dynamic presentationSpace = PresentationSpace;
            presentationSpace.SendKeys(PcommKeyHelper.ToSendKeysValue(key));
        }

        public bool WaitForText(string text, int timeoutMs, int intervalMs)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Text is required.", nameof(text));
            }

            if (timeoutMs < 0)
            {
                timeoutMs = 0;
            }

            if (intervalMs < 1)
            {
                intervalMs = 1;
            }

            dynamic presentationSpace = PresentationSpace;
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds <= timeoutMs)
            {
                try
                {
                    if (IsWaitForStringSuccess(presentationSpace.WaitForString(text, intervalMs)))
                    {
                        return true;
                    }
                }
                catch
                {
                    // WaitForString may fail transiently; retry until timeout is reached.
                }

                if (stopwatch.ElapsedMilliseconds >= timeoutMs)
                {
                    break;
                }
            }

            throw new TimeoutException(
                "Timed out after " + timeoutMs + " ms waiting for text: " + text);
        }

        public int ParallelFindText(IList<string> texts, int timeoutMs, int intervalMs)
        {
            if (texts == null || texts.Count == 0)
            {
                throw new ArgumentException("At least one candidate text is required.", nameof(texts));
            }

            if (timeoutMs < 0)
            {
                timeoutMs = 0;
            }

            if (intervalMs < 1)
            {
                intervalMs = 1;
            }

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds <= timeoutMs)
            {
                var screen = ReadAllRows();
                for (var i = 0; i < texts.Count; i++)
                {
                    var candidate = texts[i];
                    if (!string.IsNullOrEmpty(candidate) &&
                        screen.IndexOf(candidate, StringComparison.Ordinal) >= 0)
                    {
                        return i;
                    }
                }

                Thread.Sleep(intervalMs);
            }

            throw new TimeoutException(
                "Timed out after " + timeoutMs + " ms waiting for any candidate text on screen.");
        }

        public int ParallelFindTextInRow(object[,] candidates, int timeoutMs, int intervalMs)
        {
            if (candidates == null || candidates.GetLength(0) == 0)
            {
                throw new ArgumentException("At least one row/text candidate is required.", nameof(candidates));
            }

            if (timeoutMs < 0)
            {
                timeoutMs = 0;
            }

            if (intervalMs < 1)
            {
                intervalMs = 1;
            }

            var pairs = new List<KeyValuePair<int, string>>();
            for (var i = 0; i < candidates.GetLength(0); i++)
            {
                var row = Convert.ToInt32(candidates[i, 0]);
                var text = candidates[i, 1]?.ToString() ?? string.Empty;
                pairs.Add(new KeyValuePair<int, string>(row, text));
            }

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds <= timeoutMs)
            {
                for (var i = 0; i < pairs.Count; i++)
                {
                    var pair = pairs[i];
                    if (string.IsNullOrEmpty(pair.Value))
                    {
                        continue;
                    }

                    var rowText = ReadSingleRow(pair.Key);
                    if (rowText.IndexOf(pair.Value, StringComparison.Ordinal) >= 0)
                    {
                        return i;
                    }
                }

                Thread.Sleep(intervalMs);
            }

            throw new TimeoutException(
                "Timed out after " + timeoutMs + " ms waiting for any candidate text in the specified rows.");
        }

        public bool WaitForTextInRow(string text, int row, int? startCol, int? endCol, int timeoutMs, int intervalMs)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Text is required.", nameof(text));
            }

            if (timeoutMs < 0)
            {
                timeoutMs = 0;
            }

            if (intervalMs < 1)
            {
                intervalMs = 1;
            }

            dynamic presentationSpace = PresentationSpace;

            var startIndex = startCol ?? 1;
            if (startIndex < 1)
            {
                startIndex = 1;
            }

            var numCols = GetNumCols(presentationSpace);

            var endIndex = endCol ?? numCols;
            if (endIndex < 1)
            {
                endIndex = numCols;
            }

            if (endIndex > numCols)
            {
                endIndex = numCols;
            }

            if (startIndex > endIndex)
            {
                startIndex = endIndex;
            }

            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds <= timeoutMs)
            {
                try
                {
                    if (IsWaitForStringSuccess(
                        presentationSpace.WaitForStringInRect(text, row, startIndex, row, endIndex, intervalMs)))
                    {
                        return true;
                    }
                }
                catch
                {
                    // WaitForStringInRect may fail transiently; retry until timeout is reached.
                }

                if (stopwatch.ElapsedMilliseconds >= timeoutMs)
                {
                    break;
                }
            }

            throw new TimeoutException(
                "Timed out after " + timeoutMs + " ms waiting for text '" + text +
                "' in row " + row + ", columns " + startIndex + "-" + endIndex + ".");
        }

        private static bool IsWaitForStringSuccess(object result)
        {
            if (result == null)
            {
                return false;
            }

            if (result is bool booleanResult)
            {
                return booleanResult;
            }

            try
            {
                return Convert.ToInt32(result) != 0;
            }
            catch
            {
                return false;
            }
        }

        private static int GetNumCols(dynamic presentationSpace)
        {
            try
            {
                return (int)presentationSpace.NumCols;
            }
            catch
            {
                return 80;
            }
        }

        private static int GetNumRows(dynamic presentationSpace)
        {
            try
            {
                return (int)presentationSpace.NumRows;
            }
            catch
            {
                return 24;
            }
        }
    }

}
