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
        private readonly Stream _ownedLoadStream;
        private bool _disposed;

        public ExcelWorkbook(XLWorkbook workbook, string filePath, string activeSheetName)
            : this(workbook, filePath, activeSheetName, ownedLoadStream: null)
        {
        }

        /// <param name="ownedLoadStream">
        /// Stream passed to <see cref="XLWorkbook"/> constructor. ClosedXML keeps it for Save/SaveAs
        /// (to preserve unsupported parts); must stay open until this handle is disposed.
        /// </param>
        public ExcelWorkbook(
            XLWorkbook workbook,
            string filePath,
            string activeSheetName,
            Stream ownedLoadStream)
        {
            XlWorkbook = workbook ?? throw new ArgumentNullException(nameof(workbook));
            FilePath = filePath;
            ActiveSheetName = string.IsNullOrWhiteSpace(activeSheetName)
                ? workbook.Worksheet(1).Name
                : activeSheetName.Trim();
            _ownedLoadStream = ownedLoadStream;
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
            try
            {
                XlWorkbook?.Dispose();
            }
            finally
            {
                XlWorkbook = null;
                try
                {
                    _ownedLoadStream?.Dispose();
                }
                catch
                {
                    // XLWorkbook.Dispose may already have closed the stream.
                }
            }
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
