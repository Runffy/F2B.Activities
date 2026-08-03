using System;
using System.IO;
using System.Runtime.InteropServices;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    internal static class WordCom
    {
        internal const int WdFormatXmlDocument = 12;
        internal const int WdFormatDocument = 0;
        internal const int WdExportFormatPdf = 17;
        internal const int WdPageBreak = 7;
        internal const int WdAlertsNone = 0;
        internal const int WdCollapseEnd = 0;
        internal const int WdCollapseStart = 1;
        internal const int WdFindContinue = 1;
        internal const int WdReplaceNone = 0;
        internal const int MsoFalse = 0;

        internal static InteropWord.Application TryGetRunningWordApplication()
        {
            try
            {
                return (InteropWord.Application)Marshal.GetActiveObject("Word.Application");
            }
            catch (COMException)
            {
                return null;
            }
            catch (InvalidCastException)
            {
                return null;
            }
        }

        internal static InteropWord.Application GetOrCreateApplication(bool visible, out bool weCreatedApplication)
        {
            var application = TryGetRunningWordApplication();
            if (application != null)
            {
                weCreatedApplication = false;
                return application;
            }

            application = new InteropWord.Application();
            weCreatedApplication = true;
            application.Visible = visible;
            return application;
        }

        internal static InteropWord.Document TryFindOpenDocument(InteropWord.Application application, string wordFilePath)
        {
            InteropWord.Documents documents = null;
            try
            {
                documents = application.Documents;
                for (var i = 1; i <= documents.Count; i++)
                {
                    InteropWord.Document candidate = null;
                    try
                    {
                        candidate = documents[i];
                        var fullName = candidate.FullName;
                        if (string.IsNullOrWhiteSpace(fullName))
                        {
                            continue;
                        }

                        string candidatePath;
                        try
                        {
                            candidatePath = Path.GetFullPath(fullName);
                        }
                        catch
                        {
                            continue;
                        }

                        if (string.Equals(candidatePath, wordFilePath, StringComparison.OrdinalIgnoreCase))
                        {
                            var matched = candidate;
                            candidate = null;
                            return matched;
                        }
                    }
                    finally
                    {
                        ReleaseComObject(candidate);
                    }
                }
            }
            finally
            {
                ReleaseComObject(documents);
            }

            return null;
        }

        internal static InteropWord.Document OpenOrCreateDocument(
            InteropWord.Application application,
            string wordFilePath,
            bool visible,
            bool createIfMissing,
            out bool attachedToAlreadyOpenDocument,
            out bool createdNewDocument)
        {
            attachedToAlreadyOpenDocument = false;
            createdNewDocument = false;

            var existing = TryFindOpenDocument(application, wordFilePath);
            if (existing != null)
            {
                attachedToAlreadyOpenDocument = true;
                return existing;
            }

            application.Visible = visible;

            if (File.Exists(wordFilePath))
            {
                return application.Documents.Open(
                    FileName: wordFilePath,
                    ConfirmConversions: false,
                    ReadOnly: false,
                    AddToRecentFiles: false);
            }

            if (!createIfMissing)
            {
                throw new FileNotFoundException("Word file was not found: " + wordFilePath, wordFilePath);
            }

            WordActivityHelper.EnsureParentDirectoryExists(wordFilePath);
            createdNewDocument = true;
            return application.Documents.Add();
        }

        internal static void SaveDocument(InteropWord.Document document, string saveAsPathIfNew)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (!string.IsNullOrWhiteSpace(saveAsPathIfNew) && IsUnsavedOrDifferentPath(document, saveAsPathIfNew))
            {
                SaveAsDocx(document, saveAsPathIfNew);
                return;
            }

            document.Save();
        }

        internal static bool IsUnsavedOrDifferentPath(InteropWord.Document document, string wordFilePath)
        {
            try
            {
                var fullName = document.FullName;
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    return true;
                }

                return !string.Equals(
                    Path.GetFullPath(fullName),
                    Path.GetFullPath(wordFilePath),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return true;
            }
        }

        internal static void SaveAsDocx(InteropWord.Document document, string wordFilePath)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (string.IsNullOrWhiteSpace(wordFilePath))
            {
                throw new ArgumentException("Word file path is required.", nameof(wordFilePath));
            }

            WordActivityHelper.EnsureParentDirectoryExists(wordFilePath);
            TryPrepareTargetFileForSaveAs(document, wordFilePath);

            var fileFormat = ResolveNativeWordSaveFormat(wordFilePath);

            try
            {
                object fileName = wordFilePath;
                object format = fileFormat;
                object addToRecentFiles = false;
                object missing = Type.Missing;

                try
                {
                    // Prefer SaveAs2. Call via DocumentClass / reflection because some
                    // Interop Document/_Document facades used at compile time omit SaveAs2.
                    if (!TrySaveAs2(document, wordFilePath, fileFormat))
                    {
                        throw new COMException("SaveAs2 is unavailable.");
                    }

                    return;
                }
                catch (COMException)
                {
                    // Older Word / some hosts only support SaveAs.
                }

                fileName = wordFilePath;
                format = fileFormat;
                addToRecentFiles = false;
                ((InteropWord._Document)document).SaveAs(
                    ref fileName,
                    ref format,
                    ref missing,
                    ref missing,
                    ref addToRecentFiles,
                    ref missing,
                    ref missing,
                    ref missing,
                    ref missing,
                    ref missing,
                    ref missing,
                    ref missing,
                    ref missing,
                    ref missing,
                    ref missing,
                    ref missing);
            }
            catch (COMException ex)
            {
                throw new InvalidOperationException(
                    "Word SaveAs failed for path: " + wordFilePath +
                    " (HRESULT=0x" + ex.HResult.ToString("X8") + "). " + ex.Message,
                    ex);
            }
        }

        private static bool TrySaveAs2(InteropWord.Document document, string wordFilePath, object fileFormat)
        {
            object fileName = wordFilePath;
            object format = fileFormat;
            object addToRecentFiles = false;
            object missing = Type.Missing;
            object compatibilityMode = missing;

            var args = new object[]
            {
                fileName,
                format,
                missing,
                missing,
                addToRecentFiles,
                missing,
                missing,
                missing,
                missing,
                missing,
                missing,
                missing,
                missing,
                missing,
                missing,
                missing,
                compatibilityMode
            };

            var method = document.GetType().GetMethod(
                "SaveAs2",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (method == null)
            {
                return false;
            }

            try
            {
                method.Invoke(document, args);
                return true;
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is COMException)
            {
                throw (COMException)ex.InnerException;
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                {
                    throw ex.InnerException;
                }

                throw;
            }
        }

        /// <summary>
        /// With DisplayAlerts=None, overwrite conflicts often surface as generic "Command failed".
        /// Delete an existing target file when it is not the current document path.
        /// </summary>
        private static void TryPrepareTargetFileForSaveAs(InteropWord.Document document, string wordFilePath)
        {
            if (!File.Exists(wordFilePath))
            {
                return;
            }

            try
            {
                var fullName = document.FullName;
                if (!string.IsNullOrWhiteSpace(fullName) &&
                    string.Equals(
                        Path.GetFullPath(fullName),
                        Path.GetFullPath(wordFilePath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            catch
            {
                // New unsaved docs may throw on FullName; treat as safe to replace target.
            }

            try
            {
                File.Delete(wordFilePath);
            }
            catch
            {
                // Leave for Word SaveAs; failure will include the path in the wrapped exception.
            }
        }

        /// <summary>
        /// Maps path extension to Word SaveAs format. .docx (default) → XML document; .doc → binary document.
        /// </summary>
        internal static object ResolveNativeWordSaveFormat(string wordFilePath)
        {
            var extension = Path.GetExtension(wordFilePath ?? string.Empty);
            if (string.IsNullOrEmpty(extension) ||
                string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase))
            {
                return WdFormatXmlDocument;
            }

            if (string.Equals(extension, ".doc", StringComparison.OrdinalIgnoreCase))
            {
                return WdFormatDocument;
            }

            throw new ArgumentException(
                "Cannot SaveAs Word document with extension '" + extension +
                "'. Use .docx or .doc. Path: " + wordFilePath);
        }

        internal static void ReleaseComObject(object comObject)
        {
            if (comObject == null)
            {
                return;
            }

            try
            {
                Marshal.FinalReleaseComObject(comObject);
            }
            catch
            {
                // Ignore RCW release failures.
            }
        }
    }
}
