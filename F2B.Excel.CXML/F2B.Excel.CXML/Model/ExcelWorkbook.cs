using System;
using System.IO;
using ClosedXML.Excel;

namespace F2B.Excel.CXML
{
    /// <summary>
    /// Workbook session handle for workflow variables (InOutArgument).
    /// </summary>
    public sealed class ExcelWorkbook : IDisposable
    {
        private bool _disposed;

        public ExcelWorkbook(XLWorkbook workbook, string filePath, string activeSheetName)
        {
            XlWorkbook = workbook ?? throw new ArgumentNullException(nameof(workbook));
            FilePath = filePath;
            ActiveSheetName = string.IsNullOrWhiteSpace(activeSheetName)
                ? workbook.Worksheet(1).Name
                : activeSheetName.Trim();
        }

        public XLWorkbook XlWorkbook { get; private set; }

        public string FilePath { get; set; }

        public string ActiveSheetName { get; set; }

        public bool IsDirty { get; set; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            XlWorkbook?.Dispose();
            XlWorkbook = null;
        }

        internal void EnsureNotDisposed()
        {
            if (_disposed || XlWorkbook == null)
            {
                throw new ObjectDisposedException(nameof(ExcelWorkbook));
            }
        }
    }
}
