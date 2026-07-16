using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    [DisplayName("Insert Page Break")]
    [Description("Insert a page break at document start/end, or before/after a keyword or bookmark paragraph.")]
    [Designer(typeof(InsertPageBreakActivityDesigner))]
    public sealed class InsertPageBreakActivity : CodeActivity
    {
        public InsertPageBreakActivity()
        {
            DisplayName = "Insert Page Break";
            LocateMode = WordPageBreakLocateMode.DocumentEnd;
            RelativePosition = WordInsertRelativePosition.After;
            ThrowIfNotFound = true;
        }

        [DisplayName("Word File Path")]
        [Category("Input.A")]
        public InArgument<string> WordFilePath { get; set; }

        [DisplayName("Document")]
        [Category("Input.B")]
        public InOutArgument<InteropWord.Document> Document { get; set; }

        [DisplayName("Locate Mode")]
        [Category("Input.C")]
        [DefaultValue(WordPageBreakLocateMode.DocumentEnd)]
        public WordPageBreakLocateMode LocateMode { get; set; }

        [DisplayName("Relative Position")]
        [Category("Input.D")]
        [DefaultValue(WordInsertRelativePosition.After)]
        public WordInsertRelativePosition RelativePosition { get; set; }

        [DisplayName("Keyword")]
        [Category("Input.E")]
        public InArgument<string> Keyword { get; set; }

        [DisplayName("Bookmark Name")]
        [Category("Input.F")]
        public InArgument<string> BookmarkName { get; set; }

        [DisplayName("Throw If Not Found")]
        [Category("Input.G")]
        [DefaultValue(true)]
        public InArgument<bool> ThrowIfNotFound { get; set; } = true;

        [DisplayName("Visible")]
        [Category("Input.H")]
        [DefaultValue(false)]
        public InArgument<bool> Visible { get; set; } = false;

        protected override void Execute(CodeActivityContext context)
        {
            var path = WordActivityHelper.GetOptionalPath(WordFilePath, context);
            var existing = WordActivityHelper.GetOptionalDocument(Document, context);
            var keyword = WordActivityHelper.IsBound(Keyword) ? Keyword.Get(context) : null;
            string bookmarkName = null;
            if (WordActivityHelper.IsBound(BookmarkName))
            {
                bookmarkName = BookmarkName.Get(context);
                bookmarkName = string.IsNullOrWhiteSpace(bookmarkName) ? null : bookmarkName.Trim();
            }

            var throwIfNotFound = WordActivityHelper.GetOrDefault(ThrowIfNotFound, context, true);
            var visible = WordActivityHelper.GetOrDefault(Visible, context, false);
            var documentBound = WordActivityHelper.IsBound(Document);

            using (var session = WordDocumentSession.Acquire(path, existing, visible, createIfMissing: false, documentBound))
            {
                WordDocumentOperations.InsertPageBreak(
                    session.Document,
                    LocateMode,
                    RelativePosition,
                    keyword,
                    bookmarkName,
                    throwIfNotFound);
                WordActivityHelper.SetDocument(Document, context, session.Document);
                session.Complete();
            }
        }
    }
}
