using System;
using System.Diagnostics;

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

        public void SetCursorPos(int x, int y)
        {
            dynamic presentationSpace = PresentationSpace;
            presentationSpace.SetCursorPos(x, y);
        }

        public void InputText(int x, int y, string text)
        {
            SetCursorPos(x, y);
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
