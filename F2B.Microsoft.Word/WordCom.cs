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
            WordActivityHelper.EnsureParentDirectoryExists(wordFilePath);
            object fileName = wordFilePath;
            object fileFormat = WdFormatXmlDocument;
            object missing = Type.Missing;
            ((InteropWord._Document)document).SaveAs(
                ref fileName,
                ref fileFormat,
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
                ref missing,
                ref missing,
                ref missing,
                ref missing);
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
