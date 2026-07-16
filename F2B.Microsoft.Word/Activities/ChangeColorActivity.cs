using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    [DisplayName("Change Color")]
    [Description("Change font color for keyword matches, or for whole paragraphs containing the keyword.")]
    [Designer(typeof(ChangeColorActivityDesigner))]
    public sealed class ChangeColorActivity : CodeActivity
    {
        public ChangeColorActivity()
        {
            DisplayName = "Change Color";
            ColorMode = WordColorMode.Named;
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

        [DisplayName("Count")]
        [Description("How many matches to change. 0 means all matches.")]
        [Category("Input.E")]
        [DefaultValue(0)]
        public InArgument<int> Count { get; set; } = 0;

        [DisplayName("Match Case")]
        [Category("Input.F")]
        [DefaultValue(false)]
        public InArgument<bool> MatchCase { get; set; } = false;

        [DisplayName("Color Mode")]
        [Category("Input.G")]
        [DefaultValue(WordColorMode.Named)]
        public WordColorMode ColorMode { get; set; }

        [DisplayName("Color Name")]
        [Description("Word named color, for example Red or Blue.")]
        [Category("Input.H")]
        public InArgument<string> ColorName { get; set; }

        [DisplayName("RGB")]
        [Description("RGB color as #RRGGBB or R,G,B.")]
        [Category("Input.I")]
        public InArgument<string> Rgb { get; set; }

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
            var keyword = WordActivityHelper.RequireNonEmpty(Keyword, context, nameof(Keyword));
            var applyToWholeParagraph = WordActivityHelper.GetOrDefault(ApplyToWholeParagraph, context, false);
            var count = WordActivityHelper.GetOrDefault(Count, context, 0);
            var matchCase = WordActivityHelper.GetOrDefault(MatchCase, context, false);
            var colorName = WordActivityHelper.IsBound(ColorName) ? ColorName.Get(context) : null;
            var rgb = WordActivityHelper.IsBound(Rgb) ? Rgb.Get(context) : null;
            var throwIfNotFound = WordActivityHelper.GetOrDefault(ThrowIfNotFound, context, true);
            var visible = WordActivityHelper.GetOrDefault(Visible, context, false);
            var documentBound = WordActivityHelper.IsBound(Document);

            using (var session = WordDocumentSession.Acquire(path, existing, visible, createIfMissing: false, documentBound))
            {
                WordDocumentOperations.ChangeColor(
                    session.Document,
                    keyword,
                    applyToWholeParagraph,
                    count,
                    matchCase,
                    ColorMode,
                    colorName,
                    rgb,
                    throwIfNotFound);
                WordActivityHelper.SetDocument(Document, context, session.Document);
                session.Complete();
            }
        }
    }
}
