using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    [DisplayName("Append Paragraph")]
    [Description("Append a plain-text paragraph to the end of a Word document. Creates the document if it does not exist.")]
    [Designer(typeof(WordSimpleFieldsActivityDesigner))]
    public sealed class AppendParagraphActivity : CodeActivity
    {
        public AppendParagraphActivity()
        {
            DisplayName = "Append Paragraph";
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

        [DisplayName("Visible")]
        [Category("Input.D")]
        [DefaultValue(false)]
        public InArgument<bool> Visible { get; set; } = false;

        protected override void Execute(CodeActivityContext context)
        {
            var path = WordActivityHelper.GetOptionalPath(WordFilePath, context);
            var existing = WordActivityHelper.GetOptionalDocument(Document, context);
            var text = WordActivityHelper.GetOrDefault(Text, context, string.Empty) ?? string.Empty;
            var visible = WordActivityHelper.GetOrDefault(Visible, context, false);
            var documentBound = WordActivityHelper.IsBound(Document);

            using (var session = WordDocumentSession.Acquire(path, existing, visible, createIfMissing: true, documentBound))
            {
                WordDocumentOperations.AppendParagraph(session.Document, text);
                WordActivityHelper.SetDocument(Document, context, session.Document);
                session.Complete();
            }
        }
    }
}
