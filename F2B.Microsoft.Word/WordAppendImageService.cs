using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    internal static class WordAppendImageService
    {
        private const int WdFormatXmlDocument = 12;
        private const int WdAlertsNone = 0;
        private const int WdCollapseEnd = 0;
        private const int MsoFalse = 0;

        internal static void AppendImages(
            string wordFilePath,
            IReadOnlyList<string> imagePaths,
            WordImageSizeMode sizeMode,
            double customWidth,
            double customHeight,
            WordImageUnit unit,
            bool visible)
        {
            if (imagePaths == null || imagePaths.Count == 0)
            {
                throw new ArgumentException("At least one image path is required.");
            }

            wordFilePath = WordActivityHelper.NormalizeWordFilePath(wordFilePath);
            WordActivityHelper.EnsureParentDirectoryExists(wordFilePath);

            InteropWord.Application application = null;
            InteropWord.Document document = null;
            var weCreatedApplication = false;
            var attachedToAlreadyOpenDocument = false;
            var createdNewDocument = false;
            var originalDisplayAlerts = WdAlertsNone;
            var originalScreenUpdating = true;

            try
            {
                application = TryGetRunningWordApplication();
                if (application == null)
                {
                    application = new InteropWord.Application();
                    weCreatedApplication = true;
                }

                originalDisplayAlerts = (int)application.DisplayAlerts;
                originalScreenUpdating = application.ScreenUpdating;
                application.DisplayAlerts = (InteropWord.WdAlertLevel)WdAlertsNone;
                application.ScreenUpdating = false;

                document = TryFindOpenDocument(application, wordFilePath);
                if (document != null)
                {
                    attachedToAlreadyOpenDocument = true;
                }
                else
                {
                    application.Visible = visible;

                    if (File.Exists(wordFilePath))
                    {
                        document = application.Documents.Open(
                            FileName: wordFilePath,
                            ConfirmConversions: false,
                            ReadOnly: false,
                            AddToRecentFiles: false);
                    }
                    else
                    {
                        document = application.Documents.Add();
                        createdNewDocument = true;
                    }
                }

                foreach (var imagePath in imagePaths)
                {
                    AppendOneImage(document, imagePath, sizeMode, customWidth, customHeight, unit);
                }

                if (attachedToAlreadyOpenDocument)
                {
                    document.Save();
                    return;
                }

                if (createdNewDocument)
                {
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
                else
                {
                    document.Save();
                }

                document.Close(SaveChanges: false);
                document = null;

                if (weCreatedApplication)
                {
                    application.Quit(SaveChanges: false);
                    application = null;
                }
            }
            finally
            {
                try
                {
                    if (application != null)
                    {
                        application.DisplayAlerts = (InteropWord.WdAlertLevel)originalDisplayAlerts;
                        application.ScreenUpdating = originalScreenUpdating;
                    }
                }
                catch
                {
                    // Ignore restore failures during cleanup.
                }

                if (document != null && !attachedToAlreadyOpenDocument)
                {
                    try
                    {
                        document.Close(SaveChanges: false);
                    }
                    catch
                    {
                        // Ignore close failures during cleanup.
                    }
                }

                ReleaseComObject(document);
                document = null;

                if (weCreatedApplication && application != null)
                {
                    try
                    {
                        application.Quit(SaveChanges: false);
                    }
                    catch
                    {
                        // Ignore quit failures during cleanup.
                    }

                    ReleaseComObject(application);
                    application = null;
                }
            }
        }

        private static void AppendOneImage(
            InteropWord.Document document,
            string imagePath,
            WordImageSizeMode sizeMode,
            double customWidth,
            double customHeight,
            WordImageUnit unit)
        {
            var endRange = document.Content;
            endRange.Collapse(Direction: WdCollapseEnd);
            endRange.InsertParagraphAfter();

            endRange = document.Content;
            endRange.Collapse(Direction: WdCollapseEnd);

            var shape = endRange.InlineShapes.AddPicture(
                FileName: imagePath,
                LinkToFile: false,
                SaveWithDocument: true);

            ApplySize(document, shape, sizeMode, customWidth, customHeight, unit);
            ReleaseComObject(shape);
            ReleaseComObject(endRange);
        }

        private static void ApplySize(
            InteropWord.Document document,
            InteropWord.InlineShape shape,
            WordImageSizeMode sizeMode,
            double customWidth,
            double customHeight,
            WordImageUnit unit)
        {
            switch (sizeMode)
            {
                case WordImageSizeMode.RegularSize:
                    return;

                case WordImageSizeMode.AutoFit:
                    ApplyAutoFit(document, shape);
                    return;

                case WordImageSizeMode.Custom:
                    SetLockAspectRatio(shape, false);
                    shape.Width = WordActivityHelper.ToPoints(customWidth, unit);
                    shape.Height = WordActivityHelper.ToPoints(customHeight, unit);
                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(sizeMode), sizeMode, "Unsupported size mode.");
            }
        }

        private static void ApplyAutoFit(InteropWord.Document document, InteropWord.InlineShape shape)
        {
            var pageSetup = document.PageSetup;
            try
            {
                var maxWidth = (float)(pageSetup.PageWidth - pageSetup.LeftMargin - pageSetup.RightMargin);
                var maxHeight = (float)(pageSetup.PageHeight - pageSetup.TopMargin - pageSetup.BottomMargin);
                if (maxWidth <= 0 || maxHeight <= 0)
                {
                    return;
                }

                var width = shape.Width;
                var height = shape.Height;
                if (width <= 0 || height <= 0)
                {
                    return;
                }

                var scale = Math.Min(maxWidth / width, maxHeight / height);
                if (scale >= 1f)
                {
                    return;
                }

                SetLockAspectRatio(shape, true);
                shape.Width = width * scale;
                shape.Height = height * scale;
            }
            finally
            {
                ReleaseComObject(pageSetup);
            }
        }

        private static void SetLockAspectRatio(InteropWord.InlineShape shape, bool locked)
        {
            try
            {
                shape.GetType().InvokeMember(
                    "LockAspectRatio",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.SetProperty,
                    null,
                    shape,
                    new object[] { locked ? 1 : MsoFalse });
            }
            catch
            {
                // Best effort when Office.Core interop is unavailable.
            }
        }

        private static InteropWord.Application TryGetRunningWordApplication()
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

        private static InteropWord.Document TryFindOpenDocument(InteropWord.Application application, string wordFilePath)
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

        private static void ReleaseComObject(object comObject)
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
