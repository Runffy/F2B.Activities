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

            // ClosedXML keeps the constructor stream as _originalStream and reads it again
            // on Save/SaveAs. Disposing the FileStream in a using-block causes
            // "Cannot access a closed file". Load into a MemoryStream we own for the
            // workbook lifetime so the on-disk file is not held locked after open.
            byte[] bytes;
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var buffer = new MemoryStream())
                {
                    fileStream.CopyTo(buffer);
                    bytes = buffer.ToArray();
                }
            }

            var loadStream = new MemoryStream(bytes, writable: true);
            try
            {
                var xl = new XLWorkbook(loadStream);
                string active = xl.Worksheet(1).Name;
                return new ExcelWorkbook(xl, path, active, loadStream);
            }
            catch
            {
                loadStream.Dispose();
                throw;
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
