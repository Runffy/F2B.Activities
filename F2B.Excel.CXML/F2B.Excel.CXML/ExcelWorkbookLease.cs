using System;

namespace F2B.Excel.CXML
{
    /// <summary>
    /// Workbook lifetime for one activity:
    /// ① FilePath only → ephemeral: read closes; write saves then closes.
    /// ② FilePath + bound Workbook → open, assign handle, keep open.
    /// ③ Workbook only → attach existing, keep open.
    /// </summary>
    internal sealed class ExcelWorkbookLease : IDisposable
    {
        private readonly bool _ephemeral;
        private readonly bool _saveOnClose;
        private bool _disposed;

        private ExcelWorkbookLease(ExcelWorkbook workbook, bool ephemeral, bool saveOnClose)
        {
            Workbook = workbook ?? throw new ArgumentNullException(nameof(workbook));
            _ephemeral = ephemeral;
            _saveOnClose = saveOnClose;
        }

        public ExcelWorkbook Workbook { get; }

        /// <summary>True when this activity opened via FilePath and Workbook was not bound.</summary>
        public bool IsEphemeral => _ephemeral;

        public static ExcelWorkbookLease Acquire(
            ExcelWorkbook existing,
            string filePath,
            string password,
            bool workbookArgumentBound,
            Action<ExcelWorkbook> assignWorkbookIfBound,
            bool writes)
        {
            if (existing != null)
            {
                // Mode ③ (or Workbook already set from a prior open).
                existing.EnsureNotDisposed();
                if (workbookArgumentBound && assignWorkbookIfBound != null)
                {
                    assignWorkbookIfBound(existing);
                }

                return new ExcelWorkbookLease(existing, ephemeral: false, saveOnClose: false);
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Workbook or FilePath is required.");
            }

            ExcelWorkbook opened = ExcelWorkbookSession.OpenSharedRead(filePath, password);

            if (workbookArgumentBound)
            {
                // Mode ②: open and hand out the handle; caller uses Save/Close later.
                if (assignWorkbookIfBound != null)
                {
                    assignWorkbookIfBound(opened);
                }

                return new ExcelWorkbookLease(opened, ephemeral: false, saveOnClose: false);
            }

            // Mode ①: one-shot open → operate → (save if write) → close.
            return new ExcelWorkbookLease(opened, ephemeral: true, saveOnClose: writes);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!_ephemeral)
            {
                return;
            }

            ExcelOperations.Close(Workbook, saveChanges: _saveOnClose);
        }
    }
}
