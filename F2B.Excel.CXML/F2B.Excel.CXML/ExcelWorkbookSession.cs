using System;
using System.IO;
using ClosedXML.Excel;

namespace F2B.Excel.CXML
{
    /// <summary>
    /// Resolves Workbook InOut + optional FilePath (Workbook object wins when both set).
    /// </summary>
    internal sealed class ExcelWorkbookSession : IDisposable
    {
        private readonly bool _openedHere;

        private ExcelWorkbookSession(ExcelWorkbook workbook, bool openedHere)
        {
            Workbook = workbook ?? throw new ArgumentNullException(nameof(workbook));
            _openedHere = openedHere;
        }

        public ExcelWorkbook Workbook { get; }

        /// <summary>
        /// Workbook object takes priority over FilePath. When only FilePath is provided, opens and keeps it for InOut.
        /// </summary>
        public static ExcelWorkbookSession Acquire(ExcelWorkbook existing, string filePath)
        {
            if (existing != null)
            {
                existing.EnsureNotDisposed();
                return new ExcelWorkbookSession(existing, openedHere: false);
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Workbook or FilePath is required.");
            }

            string path = ExcelActivityHelper.NormalizeExcelFilePath(filePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Excel file not found: " + path, path);
            }

            ExcelWorkbook workbook = OpenSharedRead(path);
            return new ExcelWorkbookSession(workbook, openedHere: true);
        }

        public static ExcelWorkbook OpenSharedRead(string path)
        {
            path = ExcelActivityHelper.NormalizeExcelFilePath(path);
            // Allow reading while Excel has the file open (shared read).
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var xl = new XLWorkbook(stream);
                string active = xl.Worksheet(1).Name;
                return new ExcelWorkbook(xl, path, active);
            }
        }

        public void Dispose()
        {
            // Keep workbook alive for InOutArgument; caller owns lifetime via Workbook-Close.
            // _openedHere is reserved for future scoped auto-close behavior.
            GC.KeepAlive(_openedHere);
        }
    }
}
