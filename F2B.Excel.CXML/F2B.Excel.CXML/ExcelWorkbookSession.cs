using System;
using System.IO;
using ClosedXML.Excel;
using OfficeOpenXml;

namespace F2B.Excel.CXML
{
    /// <summary>
    /// Resolves Workbook InOut + optional FilePath (Workbook object wins when both set).
    /// </summary>
    internal sealed class ExcelWorkbookSession : IDisposable
    {
        // OLE Compound File Binary signature (encrypted OOXML packages).
        private static readonly byte[] OleCompoundSignature =
        {
            0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1
        };

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
        public static ExcelWorkbookSession Acquire(
            ExcelWorkbook existing,
            string filePath,
            string password = null)
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

            ExcelWorkbook workbook = OpenSharedRead(path, password);
            return new ExcelWorkbookSession(workbook, openedHere: true);
        }

        public static ExcelWorkbook OpenSharedRead(string path, string password = null)
        {
            path = ExcelActivityHelper.NormalizeExcelFilePath(path);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Excel file not found: " + path, path);
            }

            // ClosedXML keeps the constructor stream as _originalStream and reads it again
            // on Save/SaveAs. Disposing the FileStream in a using-block causes
            // "Cannot access a closed file". Load into a MemoryStream we own for the
            // workbook lifetime so the on-disk file is not held locked after open.
            byte[] bytes = ReadAllBytesShared(path);

            if (IsOleCompoundDocument(bytes))
            {
                if (string.IsNullOrEmpty(password))
                {
                    throw new InvalidOperationException(
                        "Excel file is password-protected (file-level encryption). " +
                        "Provide Password on Workbook-Open, then reuse the Workbook handle.");
                }

                try
                {
                    bytes = DecryptPackage(bytes, password);
                }
                catch (FileNotFoundException ex)
                {
                    throw new InvalidOperationException(
                        "Failed to decrypt password-protected Excel file because EPPlus.dll was not found. " +
                        "Copy EPPlus.dll next to F2B.Excel.CXML.dll (e.g. OpenRPA extensions folder).",
                        ex);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Failed to decrypt password-protected Excel file. Check the password. " +
                        "Detail: " + ex.GetType().Name + ": " + ex.Message,
                        ex);
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

        /// <summary>
        /// Decrypts an MS-OFFCRYPTO encrypted package to plain OOXML bytes via EPPlus 4,
        /// then ClosedXML loads the result. Does not keep an EPPlus workbook alive.
        /// </summary>
        private static byte[] DecryptPackage(byte[] encryptedBytes, string password)
        {
            using (var encrypted = new MemoryStream(encryptedBytes, writable: true))
            using (var package = new ExcelPackage(encrypted, password))
            {
                package.Encryption.IsEncrypted = false;
                package.Encryption.Password = null;
                return package.GetAsByteArray();
            }
        }

        private static byte[] ReadAllBytesShared(string path)
        {
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var buffer = new MemoryStream())
            {
                fileStream.CopyTo(buffer);
                return buffer.ToArray();
            }
        }

        private static bool IsOleCompoundDocument(byte[] bytes)
        {
            if (bytes == null || bytes.Length < OleCompoundSignature.Length)
            {
                return false;
            }

            for (int i = 0; i < OleCompoundSignature.Length; i++)
            {
                if (bytes[i] != OleCompoundSignature[i])
                {
                    return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            // Keep workbook alive for InOutArgument; caller owns lifetime via Workbook-Close.
            // _openedHere is reserved for future scoped auto-close behavior.
            GC.KeepAlive(_openedHere);
        }
    }
}
