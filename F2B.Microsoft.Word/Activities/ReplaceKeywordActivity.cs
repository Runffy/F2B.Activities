using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    [DisplayName("Replace Keyword")]
    [Description("Replace keyword text itself. Count=0 means all matches. Empty NewText deletes the keyword.")]
    [Designer(typeof(ReplaceKeywordActivityDesigner))]
    public sealed class ReplaceKeywordActivity : CodeActivity
    {
        public ReplaceKeywordActivity()
        {
            DisplayName = "Replace Keyword";
            Count = 0;
            MatchCase = false;
            ThrowIfNotFound = true;
        }

        [DisplayName("Word File Path")]
        [Category("Input.A")]
        public InArgument<string> WordFilePath { get; set; }

        [DisplayName("Document")]
        [Category("Input.B")]
        public InOutArgument<InteropWord.Document> Document { get; set; }

        [DisplayName("Keyword")]
        [RequiredArgument]
        [Category("Input.C")]
        public InArgument<string> Keyword { get; set; }

        [DisplayName("New Text")]
        [Description("Replacement text. Empty string deletes the keyword.")]
        [Category("Input.D")]
        public InArgument<string> NewText { get; set; }

        [DisplayName("Count")]
        [Description("How many matches to replace. 0 means all matches.")]
        [Category("Input.E")]
        [DefaultValue(0)]
        public InArgument<int> Count { get; set; } = 0;

        [DisplayName("Match Case")]
        [Category("Input.F")]
        [DefaultValue(false)]
        public InArgument<bool> MatchCase { get; set; } = false;

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
            var keyword = WordActivityHelper.RequireNonEmpty(Keyword, context, nameof(Keyword));
            var newText = WordActivityHelper.GetOrDefault(NewText, context, string.Empty) ?? string.Empty;
            var count = WordActivityHelper.GetOrDefault(Count, context, 0);
            var matchCase = WordActivityHelper.GetOrDefault(MatchCase, context, false);
            var throwIfNotFound = WordActivityHelper.GetOrDefault(ThrowIfNotFound, context, true);
            var visible = WordActivityHelper.GetOrDefault(Visible, context, false);
            var documentBound = WordActivityHelper.IsBound(Document);

            using (var session = WordDocumentSession.Acquire(path, existing, visible, createIfMissing: false, documentBound))
            {
                WordDocumentOperations.ReplaceKeyword(
                    session.Document,
                    keyword,
                    newText,
                    count,
                    matchCase,
                    throwIfNotFound);
                WordActivityHelper.SetDocument(Document, context, session.Document);
                session.Complete();
            }
        }
    }
}
