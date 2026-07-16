using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    [DisplayName("Remove Paragraph")]
    [Description("Remove a whole paragraph located by Keyword, Bookmark, or Paragraph Index.")]
    [Designer(typeof(RemoveParagraphActivityDesigner))]
    public sealed class RemoveParagraphActivity : CodeActivity
    {
        public RemoveParagraphActivity()
        {
            DisplayName = "Remove Paragraph";
            LocateMode = WordParagraphLocateMode.Keyword;
            Count = 0;
            MatchCase = false;
            ThrowIfNotFound = true;
            ParagraphIndex = 1;
        }

        [DisplayName("Word File Path")]
        [Category("Input.A")]
        public InArgument<string> WordFilePath { get; set; }

        [DisplayName("Document")]
        [Category("Input.B")]
        public InOutArgument<InteropWord.Document> Document { get; set; }

        [DisplayName("Locate Mode")]
        [Category("Input.C")]
        [DefaultValue(WordParagraphLocateMode.Keyword)]
        public WordParagraphLocateMode LocateMode { get; set; }

        [DisplayName("Keyword")]
        [Category("Input.D")]
        public InArgument<string> Keyword { get; set; }

        [DisplayName("Bookmark Name")]
        [Category("Input.E")]
        public InArgument<string> BookmarkName { get; set; }

        [DisplayName("Paragraph Index")]
        [Description("1-based paragraph index.")]
        [Category("Input.F")]
        [DefaultValue(1)]
        public InArgument<int> ParagraphIndex { get; set; } = 1;

        [DisplayName("Count")]
        [Description("For Keyword mode: how many matching paragraphs to remove. 0 means all.")]
        [Category("Input.G")]
        [DefaultValue(0)]
        public InArgument<int> Count { get; set; } = 0;

        [DisplayName("Match Case")]
        [Category("Input.H")]
        [DefaultValue(false)]
        public InArgument<bool> MatchCase { get; set; } = false;

        [DisplayName("Throw If Not Found")]
        [Category("Input.I")]
        [DefaultValue(true)]
        public InArgument<bool> ThrowIfNotFound { get; set; } = true;

        [DisplayName("Visible")]
        [Category("Input.J")]
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

            var paragraphIndex = WordActivityHelper.GetOrDefault(ParagraphIndex, context, 1);
            var count = WordActivityHelper.GetOrDefault(Count, context, 0);
            var matchCase = WordActivityHelper.GetOrDefault(MatchCase, context, false);
            var throwIfNotFound = WordActivityHelper.GetOrDefault(ThrowIfNotFound, context, true);
            var visible = WordActivityHelper.GetOrDefault(Visible, context, false);
            var documentBound = WordActivityHelper.IsBound(Document);

            using (var session = WordDocumentSession.Acquire(path, existing, visible, createIfMissing: false, documentBound))
            {
                WordDocumentOperations.RemoveParagraph(
                    session.Document,
                    LocateMode,
                    keyword,
                    bookmarkName,
                    paragraphIndex,
                    count,
                    matchCase,
                    throwIfNotFound);
                WordActivityHelper.SetDocument(Document, context, session.Document);
                session.Complete();
            }
        }
    }
}
