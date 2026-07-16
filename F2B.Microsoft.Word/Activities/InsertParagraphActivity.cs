using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    [DisplayName("Insert Paragraph")]
    [Description("Insert a plain-text paragraph before/after a bookmark or keyword, or at document start.")]
    [Designer(typeof(InsertParagraphActivityDesigner))]
    public sealed class InsertParagraphActivity : CodeActivity
    {
        public InsertParagraphActivity()
        {
            DisplayName = "Insert Paragraph";
            LocateMode = WordInsertLocateMode.Keyword;
            RelativePosition = WordInsertRelativePosition.After;
            ThrowIfNotFound = true;
        }

        [DisplayName("Word File Path")]
        [Category("Input.A")]
        public InArgument<string> WordFilePath { get; set; }

        [DisplayName("Document")]
        [Category("Input.B")]
        public InOutArgument<InteropWord.Document> Document { get; set; }

        [DisplayName("Text")]
        [RequiredArgument]
        [Category("Input.C")]
        public InArgument<string> Text { get; set; }

        [DisplayName("Locate Mode")]
        [Category("Input.D")]
        [DefaultValue(WordInsertLocateMode.Keyword)]
        public WordInsertLocateMode LocateMode { get; set; }

        [DisplayName("Relative Position")]
        [Category("Input.E")]
        [DefaultValue(WordInsertRelativePosition.After)]
        public WordInsertRelativePosition RelativePosition { get; set; }

        [DisplayName("Bookmark Name")]
        [Category("Input.F")]
        public InArgument<string> BookmarkName { get; set; }

        [DisplayName("Keyword")]
        [Category("Input.G")]
        public InArgument<string> Keyword { get; set; }

        [DisplayName("Throw If Not Found")]
        [Category("Input.H")]
        [DefaultValue(true)]
        public InArgument<bool> ThrowIfNotFound { get; set; } = true;

        [DisplayName("Visible")]
        [Category("Input.I")]
        [DefaultValue(false)]
        public InArgument<bool> Visible { get; set; } = false;

        protected override void Execute(CodeActivityContext context)
        {
            var path = WordActivityHelper.GetOptionalPath(WordFilePath, context);
            var existing = WordActivityHelper.GetOptionalDocument(Document, context);
            var text = WordActivityHelper.GetOrDefault(Text, context, string.Empty) ?? string.Empty;
            string bookmarkName = null;
            if (WordActivityHelper.IsBound(BookmarkName))
            {
                bookmarkName = BookmarkName.Get(context);
                bookmarkName = string.IsNullOrWhiteSpace(bookmarkName) ? null : bookmarkName.Trim();
            }

            var keyword = WordActivityHelper.IsBound(Keyword) ? Keyword.Get(context) : null;
            var throwIfNotFound = WordActivityHelper.GetOrDefault(ThrowIfNotFound, context, true);
            var visible = WordActivityHelper.GetOrDefault(Visible, context, false);
            var documentBound = WordActivityHelper.IsBound(Document);

            using (var session = WordDocumentSession.Acquire(path, existing, visible, createIfMissing: false, documentBound))
            {
                WordDocumentOperations.InsertParagraph(
                    session.Document,
                    text,
                    LocateMode,
                    RelativePosition,
                    bookmarkName,
                    keyword,
                    throwIfNotFound);
                WordActivityHelper.SetDocument(Document, context, session.Document);
                session.Complete();
            }
        }
    }
}
