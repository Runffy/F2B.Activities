using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    [DisplayName("Save Document")]
    [Description("Save a Word Document. Provide Document and/or WordFilePath of an already open document.")]
    [Designer(typeof(WordSimpleFieldsActivityDesigner))]
    public sealed class SaveDocumentActivity : CodeActivity
    {
        public SaveDocumentActivity()
        {
            DisplayName = "Save Document";
        }

        [DisplayName("Document")]
        [Category("Input.A")]
        public InArgument<InteropWord.Document> Document { get; set; }

        [DisplayName("Word File Path")]
        [Category("Input.B")]
        public InArgument<string> WordFilePath { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var document = WordActivityHelper.IsBound(Document) ? Document.Get(context) : null;
            var path = WordActivityHelper.GetOptionalPath(WordFilePath, context);

            if (document != null)
            {
                if (!string.IsNullOrWhiteSpace(path) && WordCom.IsUnsavedOrDifferentPath(document, path))
                {
                    WordCom.SaveAsDocx(document, WordActivityHelper.NormalizeWordFilePath(path));
                }
                else
                {
                    document.Save();
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Document or WordFilePath is required.");
            }

            path = WordActivityHelper.NormalizeWordFilePath(path);
            var application = WordCom.TryGetRunningWordApplication();
            if (application == null)
            {
                throw new InvalidOperationException("Word is not running. Open/attach the document first.");
            }

            var openDocument = WordCom.TryFindOpenDocument(application, path);
            if (openDocument == null)
            {
                throw new FileNotFoundException("Open Word document was not found for path: " + path, path);
            }

            openDocument.Save();
        }
    }
}
