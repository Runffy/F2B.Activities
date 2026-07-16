using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    [DisplayName("Set Font")]
    [Description("Set font properties for keyword matches, or whole paragraphs containing the keyword. Unset properties are left unchanged.")]
    [Designer(typeof(SetFontActivityDesigner))]
    public sealed class SetFontActivity : CodeActivity
    {
        public SetFontActivity()
        {
            DisplayName = "Set Font";
            ApplyToWholeParagraph = false;
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

        [DisplayName("Apply To Whole Paragraph")]
        [Category("Input.D")]
        [DefaultValue(false)]
        public InArgument<bool> ApplyToWholeParagraph { get; set; } = false;

        [DisplayName("Font Name")]
        [Category("Input.E")]
        public InArgument<string> FontName { get; set; }

        [DisplayName("Font Size")]
        [Description("Font size in points. Leave empty to keep unchanged.")]
        [Category("Input.F")]
        public InArgument<double> FontSize { get; set; }

        [DisplayName("Bold")]
        [Category("Input.G")]
        public InArgument<bool> Bold { get; set; }

        [DisplayName("Italic")]
        [Category("Input.H")]
        public InArgument<bool> Italic { get; set; }

        [DisplayName("Underline")]
        [Category("Input.I")]
        public InArgument<bool> Underline { get; set; }

        [DisplayName("Count")]
        [Description("How many matches to change. 0 means all matches.")]
        [Category("Input.J")]
        [DefaultValue(0)]
        public InArgument<int> Count { get; set; } = 0;

        [DisplayName("Match Case")]
        [Category("Input.K")]
        [DefaultValue(false)]
        public InArgument<bool> MatchCase { get; set; } = false;

        [DisplayName("Throw If Not Found")]
        [Category("Input.L")]
        [DefaultValue(true)]
        public InArgument<bool> ThrowIfNotFound { get; set; } = true;

        [DisplayName("Visible")]
        [Category("Input.M")]
        [DefaultValue(false)]
        public InArgument<bool> Visible { get; set; } = false;

        protected override void Execute(CodeActivityContext context)
        {
            var path = WordActivityHelper.GetOptionalPath(WordFilePath, context);
            var existing = WordActivityHelper.GetOptionalDocument(Document, context);
            var keyword = WordActivityHelper.RequireNonEmpty(Keyword, context, nameof(Keyword));
            var applyToWholeParagraph = WordActivityHelper.GetOrDefault(ApplyToWholeParagraph, context, false);
            var fontName = WordActivityHelper.IsBound(FontName) ? FontName.Get(context) : null;
            double? fontSize = WordActivityHelper.IsBound(FontSize) ? FontSize.Get(context) : (double?)null;
            bool? bold = WordActivityHelper.IsBound(Bold) ? Bold.Get(context) : (bool?)null;
            bool? italic = WordActivityHelper.IsBound(Italic) ? Italic.Get(context) : (bool?)null;
            bool? underline = WordActivityHelper.IsBound(Underline) ? Underline.Get(context) : (bool?)null;
            var count = WordActivityHelper.GetOrDefault(Count, context, 0);
            var matchCase = WordActivityHelper.GetOrDefault(MatchCase, context, false);
            var throwIfNotFound = WordActivityHelper.GetOrDefault(ThrowIfNotFound, context, true);
            var visible = WordActivityHelper.GetOrDefault(Visible, context, false);
            var documentBound = WordActivityHelper.IsBound(Document);

            using (var session = WordDocumentSession.Acquire(path, existing, visible, createIfMissing: false, documentBound))
            {
                WordDocumentOperations.SetFont(
                    session.Document,
                    keyword,
                    applyToWholeParagraph,
                    count,
                    matchCase,
                    fontName,
                    fontSize,
                    bold,
                    italic,
                    underline,
                    throwIfNotFound);
                WordActivityHelper.SetDocument(Document, context, session.Document);
                session.Complete();
            }
        }
    }
}
