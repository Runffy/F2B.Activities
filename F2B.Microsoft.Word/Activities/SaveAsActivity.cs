using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    [DisplayName("Save As")]
    [Description("Save or export the Word document as docx, doc, or pdf.")]
    [Designer(typeof(SaveAsActivityDesigner))]
    public sealed class SaveAsActivity : CodeActivity
    {
        public SaveAsActivity()
        {
            DisplayName = "Save As";
            Format = WordSaveAsFormat.Docx;
            Overwrite = true;
        }

        [DisplayName("Word File Path")]
        [Category("Input.A")]
        public InArgument<string> WordFilePath { get; set; }

        [DisplayName("Document")]
        [Category("Input.B")]
        public InOutArgument<InteropWord.Document> Document { get; set; }

        [DisplayName("Output Path")]
        [RequiredArgument]
        [Category("Input.C")]
        public InArgument<string> OutputPath { get; set; }

        [DisplayName("Format")]
        [Category("Input.D")]
        [DefaultValue(WordSaveAsFormat.Docx)]
        public WordSaveAsFormat Format { get; set; }

        [DisplayName("Overwrite")]
        [Category("Input.E")]
        [DefaultValue(true)]
        public InArgument<bool> Overwrite { get; set; } = true;

        [DisplayName("Visible")]
        [Category("Input.F")]
        [DefaultValue(false)]
        public InArgument<bool> Visible { get; set; } = false;

        protected override void Execute(CodeActivityContext context)
        {
            var path = WordActivityHelper.GetOptionalPath(WordFilePath, context);
            var existing = WordActivityHelper.GetOptionalDocument(Document, context);
            var outputPath = WordActivityHelper.RequireNonEmpty(OutputPath, context, nameof(OutputPath));
            var overwrite = WordActivityHelper.GetOrDefault(Overwrite, context, true);
            var visible = WordActivityHelper.GetOrDefault(Visible, context, false);
            var documentBound = WordActivityHelper.IsBound(Document);

            using (var session = WordDocumentSession.Acquire(path, existing, visible, createIfMissing: false, documentBound))
            {
                WordDocumentOperations.SaveAs(session.Document, outputPath, Format, overwrite);
                WordActivityHelper.SetDocument(Document, context, session.Document);
                session.Complete();
            }
        }
    }
}
