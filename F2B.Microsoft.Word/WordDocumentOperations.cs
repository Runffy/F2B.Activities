using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    internal static class WordDocumentOperations
    {
        internal static void AppendImages(
            InteropWord.Document document,
            IReadOnlyList<string> imagePaths,
            WordImageSizeMode sizeMode,
            double customWidth,
            double customHeight,
            WordImageUnit unit)
        {
            foreach (var imagePath in imagePaths)
            {
                AppendOneImage(document, imagePath, sizeMode, customWidth, customHeight, unit);
            }
        }

        internal static void AppendParagraph(InteropWord.Document document, string text)
        {
            var endRange = document.Content;
            endRange.Collapse(Direction: WordCom.WdCollapseEnd);
            endRange.InsertParagraphAfter();

            endRange = document.Content;
            endRange.Collapse(Direction: WordCom.WdCollapseEnd);
            endRange.Text = text ?? string.Empty;
            WordCom.ReleaseComObject(endRange);
        }

        internal static void InsertParagraph(
            InteropWord.Document document,
            string text,
            WordInsertLocateMode locateMode,
            WordInsertRelativePosition relativePosition,
            string bookmarkName,
            string keyword,
            bool throwIfNotFound)
        {
            text = text ?? string.Empty;

            switch (locateMode)
            {
                case WordInsertLocateMode.DocumentStart:
                    InsertAtDocumentStart(document, text);
                    return;

                case WordInsertLocateMode.Bookmark:
                    InsertAroundBookmark(document, text, bookmarkName, relativePosition, throwIfNotFound);
                    return;

                case WordInsertLocateMode.Keyword:
                    InsertAroundKeyword(document, text, keyword, relativePosition, throwIfNotFound);
                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(locateMode), locateMode, "Unsupported locate mode.");
            }
        }

        internal static void ChangeColor(
            InteropWord.Document document,
            string keyword,
            bool applyToWholeParagraph,
            int count,
            bool matchCase,
            WordColorMode colorMode,
            string colorName,
            string rgb,
            bool throwIfNotFound)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                throw new ArgumentException("Keyword is required.");
            }

            if (count < 0)
            {
                throw new ArgumentException("Count must be greater than or equal to 0.");
            }

            var applied = 0;
            var start = 1;
            while (count == 0 || applied < count)
            {
                var range = document.Content;
                range.Start = start;
                range.End = document.Content.End;

                var find = range.Find;
                find.ClearFormatting();
                find.Text = keyword;
                find.Forward = true;
                find.Wrap = (InteropWord.WdFindWrap)WordCom.WdFindContinue;
                find.MatchCase = matchCase;
                find.MatchWholeWord = false;
                find.MatchWildcards = false;

                var found = find.Execute();
                WordCom.ReleaseComObject(find);

                if (!found)
                {
                    WordCom.ReleaseComObject(range);
                    break;
                }

                if (applyToWholeParagraph)
                {
                    var paragraphRange = range.Paragraphs[1].Range;
                    ApplyColor(paragraphRange, colorMode, colorName, rgb);
                    start = paragraphRange.End;
                    WordCom.ReleaseComObject(paragraphRange);
                }
                else
                {
                    ApplyColor(range, colorMode, colorName, rgb);
                    start = range.End;
                }

                WordCom.ReleaseComObject(range);
                applied++;
            }

            if (applied == 0 && throwIfNotFound)
            {
                throw new InvalidOperationException("Keyword was not found: " + keyword);
            }
        }

        internal static void SetFont(
            InteropWord.Document document,
            string keyword,
            bool applyToWholeParagraph,
            int count,
            bool matchCase,
            string fontName,
            double? fontSize,
            bool? bold,
            bool? italic,
            bool? underline,
            bool throwIfNotFound)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                throw new ArgumentException("Keyword is required.");
            }

            if (count < 0)
            {
                throw new ArgumentException("Count must be greater than or equal to 0.");
            }

            var applied = 0;
            var start = 1;
            while (count == 0 || applied < count)
            {
                var range = document.Content;
                range.Start = start;
                range.End = document.Content.End;

                var find = range.Find;
                find.ClearFormatting();
                find.Text = keyword;
                find.Forward = true;
                find.Wrap = (InteropWord.WdFindWrap)WordCom.WdFindContinue;
                find.MatchCase = matchCase;
                find.MatchWholeWord = false;
                find.MatchWildcards = false;

                var found = find.Execute();
                WordCom.ReleaseComObject(find);

                if (!found)
                {
                    WordCom.ReleaseComObject(range);
                    break;
                }

                if (applyToWholeParagraph)
                {
                    var paragraphRange = range.Paragraphs[1].Range;
                    ApplyFont(paragraphRange, fontName, fontSize, bold, italic, underline);
                    start = paragraphRange.End;
                    WordCom.ReleaseComObject(paragraphRange);
                }
                else
                {
                    ApplyFont(range, fontName, fontSize, bold, italic, underline);
                    start = range.End;
                }

                WordCom.ReleaseComObject(range);
                applied++;
            }

            if (applied == 0 && throwIfNotFound)
            {
                throw new InvalidOperationException("Keyword was not found: " + keyword);
            }
        }

        internal static void InsertPageBreak(
            InteropWord.Document document,
            WordPageBreakLocateMode locateMode,
            WordInsertRelativePosition relativePosition,
            string keyword,
            string bookmarkName,
            bool throwIfNotFound)
        {
            switch (locateMode)
            {
                case WordPageBreakLocateMode.DocumentEnd:
                {
                    var range = document.Content;
                    range.Collapse(Direction: WordCom.WdCollapseEnd);
                    range.InsertBreak(Type: WordCom.WdPageBreak);
                    WordCom.ReleaseComObject(range);
                    return;
                }

                case WordPageBreakLocateMode.DocumentStart:
                {
                    var range = document.Content;
                    range.Collapse(Direction: WordCom.WdCollapseStart);
                    range.InsertBreak(Type: WordCom.WdPageBreak);
                    WordCom.ReleaseComObject(range);
                    return;
                }

                case WordPageBreakLocateMode.Keyword:
                    InsertPageBreakAroundKeyword(document, keyword, relativePosition, throwIfNotFound);
                    return;

                case WordPageBreakLocateMode.Bookmark:
                    InsertPageBreakAroundBookmark(document, bookmarkName, relativePosition, throwIfNotFound);
                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(locateMode), locateMode, "Unsupported locate mode.");
            }
        }

        internal static void SaveAs(
            InteropWord.Document document,
            string outputPath,
            WordSaveAsFormat format,
            bool overwrite)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("OutputPath is required.");
            }

            outputPath = Path.GetFullPath(outputPath.Trim());
            EnsureOutputExtension(ref outputPath, format);
            WordActivityHelper.EnsureParentDirectoryExists(outputPath);

            if (File.Exists(outputPath))
            {
                if (!overwrite)
                {
                    throw new IOException("Output file already exists: " + outputPath);
                }

                File.Delete(outputPath);
            }

            switch (format)
            {
                case WordSaveAsFormat.Docx:
                    WordCom.SaveAsDocx(document, outputPath);
                    return;

                case WordSaveAsFormat.Doc:
                    SaveAsDoc(document, outputPath);
                    return;

                case WordSaveAsFormat.Pdf:
                    ExportAsPdf(document, outputPath);
                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported save format.");
            }
        }

        private static void ApplyFont(
            InteropWord.Range range,
            string fontName,
            double? fontSize,
            bool? bold,
            bool? italic,
            bool? underline)
        {
            var font = range.Font;
            try
            {
                if (!string.IsNullOrWhiteSpace(fontName))
                {
                    font.Name = fontName.Trim();
                }

                if (fontSize.HasValue)
                {
                    if (fontSize.Value <= 0)
                    {
                        throw new ArgumentException("FontSize must be greater than zero.");
                    }

                    font.Size = (float)fontSize.Value;
                }

                if (bold.HasValue)
                {
                    font.Bold = bold.Value ? 1 : 0;
                }

                if (italic.HasValue)
                {
                    font.Italic = italic.Value ? 1 : 0;
                }

                if (underline.HasValue)
                {
                    font.Underline = underline.Value
                        ? InteropWord.WdUnderline.wdUnderlineSingle
                        : InteropWord.WdUnderline.wdUnderlineNone;
                }
            }
            finally
            {
                WordCom.ReleaseComObject(font);
            }
        }

        private static void InsertPageBreakAroundKeyword(
            InteropWord.Document document,
            string keyword,
            WordInsertRelativePosition relativePosition,
            bool throwIfNotFound)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                throw new ArgumentException("Keyword is required when Locate Mode is Keyword.");
            }

            var range = document.Content;
            var find = range.Find;
            find.ClearFormatting();
            find.Text = keyword;
            find.Forward = true;
            find.Wrap = (InteropWord.WdFindWrap)WordCom.WdFindContinue;
            find.MatchCase = false;
            find.MatchWholeWord = false;
            find.MatchWildcards = false;

            var found = find.Execute();
            WordCom.ReleaseComObject(find);

            if (!found)
            {
                WordCom.ReleaseComObject(range);
                if (throwIfNotFound)
                {
                    throw new InvalidOperationException("Keyword was not found: " + keyword);
                }

                return;
            }

            var paragraphRange = range.Paragraphs[1].Range;
            InsertPageBreakRelativeToParagraph(paragraphRange, relativePosition);
            WordCom.ReleaseComObject(paragraphRange);
            WordCom.ReleaseComObject(range);
        }

        private static void InsertPageBreakAroundBookmark(
            InteropWord.Document document,
            string bookmarkName,
            WordInsertRelativePosition relativePosition,
            bool throwIfNotFound)
        {
            if (string.IsNullOrWhiteSpace(bookmarkName))
            {
                throw new ArgumentException("BookmarkName is required when Locate Mode is Bookmark.");
            }

            InteropWord.Bookmark bookmark = null;
            try
            {
                var bookmarks = document.Bookmarks;
                if (!bookmarks.Exists(bookmarkName))
                {
                    WordCom.ReleaseComObject(bookmarks);
                    if (throwIfNotFound)
                    {
                        throw new InvalidOperationException("Bookmark was not found: " + bookmarkName);
                    }

                    return;
                }

                bookmark = bookmarks[bookmarkName];
                WordCom.ReleaseComObject(bookmarks);

                var paragraphRange = bookmark.Range.Paragraphs[1].Range;
                InsertPageBreakRelativeToParagraph(paragraphRange, relativePosition);
                WordCom.ReleaseComObject(paragraphRange);
            }
            finally
            {
                WordCom.ReleaseComObject(bookmark);
            }
        }

        private static void InsertPageBreakRelativeToParagraph(
            InteropWord.Range paragraphRange,
            WordInsertRelativePosition relativePosition)
        {
            var target = paragraphRange.Duplicate;
            if (relativePosition == WordInsertRelativePosition.Before)
            {
                target.Collapse(Direction: WordCom.WdCollapseStart);
            }
            else
            {
                target.Collapse(Direction: WordCom.WdCollapseEnd);
            }

            target.InsertBreak(Type: WordCom.WdPageBreak);
            WordCom.ReleaseComObject(target);
        }

        private static void EnsureOutputExtension(ref string outputPath, WordSaveAsFormat format)
        {
            var extension = Path.GetExtension(outputPath);
            string expected;
            switch (format)
            {
                case WordSaveAsFormat.Docx:
                    expected = ".docx";
                    break;
                case WordSaveAsFormat.Doc:
                    expected = ".doc";
                    break;
                case WordSaveAsFormat.Pdf:
                    expected = ".pdf";
                    break;
                default:
                    return;
            }

            if (string.IsNullOrEmpty(extension))
            {
                outputPath += expected;
                return;
            }

            if (!string.Equals(extension, expected, StringComparison.OrdinalIgnoreCase))
            {
                outputPath = Path.ChangeExtension(outputPath, expected);
            }
        }

        private static void SaveAsDoc(InteropWord.Document document, string outputPath)
        {
            object fileName = outputPath;
            object fileFormat = WordCom.WdFormatDocument;
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

        private static void ExportAsPdf(InteropWord.Document document, string outputPath)
        {
            document.ExportAsFixedFormat(
                OutputFileName: outputPath,
                ExportFormat: (InteropWord.WdExportFormat)WordCom.WdExportFormatPdf,
                OpenAfterExport: false,
                OptimizeFor: InteropWord.WdExportOptimizeFor.wdExportOptimizeForPrint,
                Range: InteropWord.WdExportRange.wdExportAllDocument,
                From: 0,
                To: 0,
                Item: InteropWord.WdExportItem.wdExportDocumentContent,
                IncludeDocProps: true,
                KeepIRM: true,
                CreateBookmarks: InteropWord.WdExportCreateBookmarks.wdExportCreateNoBookmarks,
                DocStructureTags: true,
                BitmapMissingFonts: true,
                UseISO19005_1: false);
        }

        internal static void ReplaceKeyword(
            InteropWord.Document document,
            string keyword,
            string newText,
            int count,
            bool matchCase,
            bool throwIfNotFound)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                throw new ArgumentException("Keyword is required.");
            }

            if (count < 0)
            {
                throw new ArgumentException("Count must be greater than or equal to 0.");
            }

            newText = newText ?? string.Empty;
            var applied = 0;
            var start = 1;
            while (count == 0 || applied < count)
            {
                var range = document.Content;
                range.Start = start;
                range.End = document.Content.End;

                var find = range.Find;
                find.ClearFormatting();
                find.Text = keyword;
                find.Forward = true;
                find.Wrap = (InteropWord.WdFindWrap)WordCom.WdFindContinue;
                find.MatchCase = matchCase;
                find.MatchWholeWord = false;
                find.MatchWildcards = false;

                var found = find.Execute();
                WordCom.ReleaseComObject(find);

                if (!found)
                {
                    WordCom.ReleaseComObject(range);
                    break;
                }

                var matchEnd = range.Start + newText.Length;
                range.Text = newText;
                start = Math.Max(matchEnd, range.Start);
                WordCom.ReleaseComObject(range);
                applied++;
            }

            if (applied == 0 && throwIfNotFound)
            {
                throw new InvalidOperationException("Keyword was not found: " + keyword);
            }
        }

        internal static void ReplaceParagraph(
            InteropWord.Document document,
            string newText,
            WordParagraphLocateMode locateMode,
            string keyword,
            string bookmarkName,
            int paragraphIndex,
            int count,
            bool matchCase,
            bool throwIfNotFound)
        {
            newText = newText ?? string.Empty;

            switch (locateMode)
            {
                case WordParagraphLocateMode.Keyword:
                    ReplaceOrRemoveParagraphsByKeyword(
                        document,
                        keyword,
                        count,
                        matchCase,
                        throwIfNotFound,
                        remove: false,
                        newText: newText);
                    return;

                case WordParagraphLocateMode.Bookmark:
                    ReplaceOrRemoveParagraphByBookmark(
                        document,
                        bookmarkName,
                        throwIfNotFound,
                        remove: false,
                        newText: newText);
                    return;

                case WordParagraphLocateMode.ParagraphIndex:
                    ReplaceOrRemoveParagraphByIndex(
                        document,
                        paragraphIndex,
                        throwIfNotFound,
                        remove: false,
                        newText: newText);
                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(locateMode), locateMode, "Unsupported locate mode.");
            }
        }

        internal static void RemoveParagraph(
            InteropWord.Document document,
            WordParagraphLocateMode locateMode,
            string keyword,
            string bookmarkName,
            int paragraphIndex,
            int count,
            bool matchCase,
            bool throwIfNotFound)
        {
            switch (locateMode)
            {
                case WordParagraphLocateMode.Keyword:
                    ReplaceOrRemoveParagraphsByKeyword(
                        document,
                        keyword,
                        count,
                        matchCase,
                        throwIfNotFound,
                        remove: true,
                        newText: null);
                    return;

                case WordParagraphLocateMode.Bookmark:
                    ReplaceOrRemoveParagraphByBookmark(
                        document,
                        bookmarkName,
                        throwIfNotFound,
                        remove: true,
                        newText: null);
                    return;

                case WordParagraphLocateMode.ParagraphIndex:
                    ReplaceOrRemoveParagraphByIndex(
                        document,
                        paragraphIndex,
                        throwIfNotFound,
                        remove: true,
                        newText: null);
                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(locateMode), locateMode, "Unsupported locate mode.");
            }
        }

        private static void ReplaceOrRemoveParagraphsByKeyword(
            InteropWord.Document document,
            string keyword,
            int count,
            bool matchCase,
            bool throwIfNotFound,
            bool remove,
            string newText)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                throw new ArgumentException("Keyword is required when Locate Mode is Keyword.");
            }

            if (count < 0)
            {
                throw new ArgumentException("Count must be greater than or equal to 0.");
            }

            var applied = 0;
            var start = 1;
            while (count == 0 || applied < count)
            {
                var range = document.Content;
                range.Start = start;
                range.End = document.Content.End;

                var find = range.Find;
                find.ClearFormatting();
                find.Text = keyword;
                find.Forward = true;
                find.Wrap = (InteropWord.WdFindWrap)WordCom.WdFindContinue;
                find.MatchCase = matchCase;
                find.MatchWholeWord = false;
                find.MatchWildcards = false;

                var found = find.Execute();
                WordCom.ReleaseComObject(find);

                if (!found)
                {
                    WordCom.ReleaseComObject(range);
                    break;
                }

                var paragraphRange = range.Paragraphs[1].Range;
                WordCom.ReleaseComObject(range);

                if (remove)
                {
                    start = paragraphRange.Start;
                    paragraphRange.Delete();
                }
                else
                {
                    SetParagraphText(paragraphRange, newText);
                    start = paragraphRange.End;
                }

                WordCom.ReleaseComObject(paragraphRange);
                applied++;
            }

            if (applied == 0 && throwIfNotFound)
            {
                throw new InvalidOperationException("Keyword was not found: " + keyword);
            }
        }

        private static void ReplaceOrRemoveParagraphByBookmark(
            InteropWord.Document document,
            string bookmarkName,
            bool throwIfNotFound,
            bool remove,
            string newText)
        {
            if (string.IsNullOrWhiteSpace(bookmarkName))
            {
                throw new ArgumentException("BookmarkName is required when Locate Mode is Bookmark.");
            }

            InteropWord.Bookmark bookmark = null;
            try
            {
                var bookmarks = document.Bookmarks;
                if (!bookmarks.Exists(bookmarkName))
                {
                    WordCom.ReleaseComObject(bookmarks);
                    if (throwIfNotFound)
                    {
                        throw new InvalidOperationException("Bookmark was not found: " + bookmarkName);
                    }

                    return;
                }

                bookmark = bookmarks[bookmarkName];
                WordCom.ReleaseComObject(bookmarks);

                var paragraphRange = bookmark.Range.Paragraphs[1].Range;
                if (remove)
                {
                    paragraphRange.Delete();
                }
                else
                {
                    SetParagraphText(paragraphRange, newText);
                }

                WordCom.ReleaseComObject(paragraphRange);
            }
            finally
            {
                WordCom.ReleaseComObject(bookmark);
            }
        }

        private static void ReplaceOrRemoveParagraphByIndex(
            InteropWord.Document document,
            int paragraphIndex,
            bool throwIfNotFound,
            bool remove,
            string newText)
        {
            if (paragraphIndex < 1)
            {
                throw new ArgumentException("ParagraphIndex must be greater than or equal to 1.");
            }

            var paragraphs = document.Paragraphs;
            try
            {
                if (paragraphIndex > paragraphs.Count)
                {
                    if (throwIfNotFound)
                    {
                        throw new InvalidOperationException(
                            "Paragraph index is out of range: " + paragraphIndex +
                            ". Document has " + paragraphs.Count + " paragraph(s).");
                    }

                    return;
                }

                var paragraphRange = paragraphs[paragraphIndex].Range;
                if (remove)
                {
                    paragraphRange.Delete();
                }
                else
                {
                    SetParagraphText(paragraphRange, newText);
                }

                WordCom.ReleaseComObject(paragraphRange);
            }
            finally
            {
                WordCom.ReleaseComObject(paragraphs);
            }
        }

        private static void SetParagraphText(InteropWord.Range paragraphRange, string newText)
        {
            // Keep the paragraph mark so the paragraph structure remains.
            if (paragraphRange.End > paragraphRange.Start)
            {
                var textRange = paragraphRange.Duplicate;
                if (textRange.End > textRange.Start)
                {
                    textRange.End = textRange.End - 1;
                }

                textRange.Text = newText ?? string.Empty;
                WordCom.ReleaseComObject(textRange);
                return;
            }

            paragraphRange.Text = newText ?? string.Empty;
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
            endRange.Collapse(Direction: WordCom.WdCollapseEnd);
            endRange.InsertParagraphAfter();

            endRange = document.Content;
            endRange.Collapse(Direction: WordCom.WdCollapseEnd);

            var shape = endRange.InlineShapes.AddPicture(
                FileName: imagePath,
                LinkToFile: false,
                SaveWithDocument: true);

            ApplySize(document, shape, sizeMode, customWidth, customHeight, unit);
            WordCom.ReleaseComObject(shape);
            WordCom.ReleaseComObject(endRange);
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
                WordCom.ReleaseComObject(pageSetup);
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
                    new object[] { locked ? 1 : WordCom.MsoFalse });
            }
            catch
            {
                // Best effort when Office.Core interop is unavailable.
            }
        }

        private static void InsertAtDocumentStart(InteropWord.Document document, string text)
        {
            var range = document.Content;
            range.Collapse(Direction: WordCom.WdCollapseStart);
            range.InsertParagraphBefore();
            range = document.Content;
            range.Collapse(Direction: WordCom.WdCollapseStart);
            range.Text = text;
            WordCom.ReleaseComObject(range);
        }

        private static void InsertAroundBookmark(
            InteropWord.Document document,
            string text,
            string bookmarkName,
            WordInsertRelativePosition relativePosition,
            bool throwIfNotFound)
        {
            if (string.IsNullOrWhiteSpace(bookmarkName))
            {
                throw new ArgumentException("BookmarkName is required when Locate Mode is Bookmark.");
            }

            InteropWord.Bookmark bookmark = null;
            try
            {
                var bookmarks = document.Bookmarks;
                if (!bookmarks.Exists(bookmarkName))
                {
                    WordCom.ReleaseComObject(bookmarks);
                    if (throwIfNotFound)
                    {
                        throw new InvalidOperationException("Bookmark was not found: " + bookmarkName);
                    }

                    return;
                }

                bookmark = bookmarks[bookmarkName];
                WordCom.ReleaseComObject(bookmarks);

                var anchor = bookmark.Range.Paragraphs[1].Range;
                InsertRelativeToParagraph(anchor, text, relativePosition);
                WordCom.ReleaseComObject(anchor);
            }
            finally
            {
                WordCom.ReleaseComObject(bookmark);
            }
        }

        private static void InsertAroundKeyword(
            InteropWord.Document document,
            string text,
            string keyword,
            WordInsertRelativePosition relativePosition,
            bool throwIfNotFound)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                throw new ArgumentException("Keyword is required when Locate Mode is Keyword.");
            }

            var range = document.Content;
            var find = range.Find;
            find.ClearFormatting();
            find.Text = keyword;
            find.Forward = true;
            find.Wrap = (InteropWord.WdFindWrap)WordCom.WdFindContinue;
            find.MatchCase = false;
            find.MatchWholeWord = false;
            find.MatchWildcards = false;

            var found = find.Execute();
            WordCom.ReleaseComObject(find);

            if (!found)
            {
                WordCom.ReleaseComObject(range);
                if (throwIfNotFound)
                {
                    throw new InvalidOperationException("Keyword was not found: " + keyword);
                }

                return;
            }

            var paragraphRange = range.Paragraphs[1].Range;
            InsertRelativeToParagraph(paragraphRange, text, relativePosition);
            WordCom.ReleaseComObject(paragraphRange);
            WordCom.ReleaseComObject(range);
        }

        private static void InsertRelativeToParagraph(
            InteropWord.Range paragraphRange,
            string text,
            WordInsertRelativePosition relativePosition)
        {
            if (relativePosition == WordInsertRelativePosition.Before)
            {
                var before = paragraphRange.Duplicate;
                before.Collapse(Direction: WordCom.WdCollapseStart);
                before.InsertParagraphBefore();
                before.Collapse(Direction: WordCom.WdCollapseStart);
                before.Text = text;
                WordCom.ReleaseComObject(before);
                return;
            }

            var after = paragraphRange.Duplicate;
            after.Collapse(Direction: WordCom.WdCollapseEnd);
            after.InsertParagraphAfter();
            after.Collapse(Direction: WordCom.WdCollapseEnd);
            after.Text = text;
            WordCom.ReleaseComObject(after);
        }

        private static void ApplyColor(
            InteropWord.Range range,
            WordColorMode colorMode,
            string colorName,
            string rgb)
        {
            if (colorMode == WordColorMode.Named)
            {
                range.Font.Color = WordActivityHelper.ResolveNamedColor(colorName);
                return;
            }

            range.Font.Color = (InteropWord.WdColor)WordActivityHelper.ParseRgbColor(rgb);
        }
    }
}
