using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    [DisplayName("Replace Paragraph")]
    [Description("Replace a whole paragraph located by Keyword, Bookmark, or Paragraph Index. Empty NewText clears paragraph text.")]
    [Designer(typeof(ReplaceParagraphActivityDesigner))]
    public sealed class ReplaceParagraphActivity : CodeActivity
    {
        public ReplaceParagraphActivity()
        {
            DisplayName = "Replace Paragraph";
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

        [DisplayName("New Text")]
        [Description("New paragraph text. Empty string clears the paragraph content.")]
        [Category("Input.C")]
        public InArgument<string> NewText { get; set; }

        [DisplayName("Locate Mode")]
        [Category("Input.D")]
        [DefaultValue(WordParagraphLocateMode.Keyword)]
        public WordParagraphLocateMode LocateMode { get; set; }

        [DisplayName("Keyword")]
        [Category("Input.E")]
        public InArgument<string> Keyword { get; set; }

        [DisplayName("Bookmark Name")]
        [Category("Input.F")]
        public InArgument<string> BookmarkName { get; set; }

        [DisplayName("Paragraph Index")]
        [Description("1-based paragraph index.")]
        [Category("Input.G")]
        [DefaultValue(1)]
        public InArgument<int> ParagraphIndex { get; set; } = 1;

        [DisplayName("Count")]
        [Description("For Keyword mode: how many matching paragraphs to replace. 0 means all.")]
        [Category("Input.H")]
        [DefaultValue(0)]
        public InArgument<int> Count { get; set; } = 0;

        [DisplayName("Match Case")]
        [Category("Input.I")]
        [DefaultValue(false)]
        public InArgument<bool> MatchCase { get; set; } = false;

        [DisplayName("Throw If Not Found")]
        [Category("Input.J")]
        [DefaultValue(true)]
        public InArgument<bool> ThrowIfNotFound { get; set; } = true;

        [DisplayName("Visible")]
        [Category("Input.K")]
        [DefaultValue(false)]
        public InArgument<bool> Visible { get; set; } = false;

        protected override void Execute(CodeActivityContext context)
        {
            var path = WordActivityHelper.GetOptionalPath(WordFilePath, context);
            var existing = WordActivityHelper.GetOptionalDocument(Document, context);
            var newText = WordActivityHelper.GetOrDefault(NewText, context, string.Empty) ?? string.Empty;
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
                WordDocumentOperations.ReplaceParagraph(
                    session.Document,
                    newText,
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
