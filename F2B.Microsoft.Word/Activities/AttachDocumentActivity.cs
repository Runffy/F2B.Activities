using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    [DisplayName("Attach Document")]
    [Description("Open or attach to a Word document and output the Document object. Does not close Word.")]
    [Designer(typeof(WordSimpleFieldsActivityDesigner))]
    public sealed class AttachDocumentActivity : CodeActivity
    {
        public AttachDocumentActivity()
        {
            DisplayName = "Attach Document";
        }

        [DisplayName("Word File Path")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> WordFilePath { get; set; }

        [DisplayName("Document")]
        [Category("Output")]
        public OutArgument<InteropWord.Document> Document { get; set; }

        [DisplayName("Visible")]
        [Category("Input.B")]
        [DefaultValue(false)]
        public InArgument<bool> Visible { get; set; } = false;

        protected override void Execute(CodeActivityContext context)
        {
            var path = WordActivityHelper.RequireNonEmpty(WordFilePath, context, nameof(WordFilePath));
            var visible = WordActivityHelper.GetOrDefault(Visible, context, false);
            var document = WordDocumentSession.Attach(path, visible);
            WordActivityHelper.SetDocument(Document, context, document);
        }
    }
}
