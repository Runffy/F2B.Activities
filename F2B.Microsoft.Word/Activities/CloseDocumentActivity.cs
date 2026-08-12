using System;
using System.Activities;
using System.ComponentModel;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    [DisplayName("Close Document")]
    [Description("Close a Word Document object previously opened or attached. Optionally save changes before closing.")]
    [Designer(typeof(WordSimpleFieldsActivityDesigner))]
    public sealed class CloseDocumentActivity : CodeActivity
    {
        public CloseDocumentActivity()
        {
            DisplayName = "Close Document";
            SaveChanges = new InArgument<bool>(false);
        }

        [RequiredArgument]
        [DisplayName("Document")]
        [Category("Input.A")]
        public InArgument<InteropWord.Document> Document { get; set; }

        [DisplayName("Save Changes")]
        [Description("When true, save the document before closing. Default false.")]
        [Category("Input.B")]
        [DefaultValue(false)]
        public InArgument<bool> SaveChanges { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            if (!WordActivityHelper.IsBound(Document))
            {
                throw new ArgumentException("Document is required.");
            }

            InteropWord.Document document = Document.Get(context);
            if (document == null)
            {
                throw new ArgumentException("Document is required.");
            }

            bool saveChanges = WordActivityHelper.GetOrDefault(SaveChanges, context, false);

            try
            {
                document.Close(SaveChanges: saveChanges);
            }
            finally
            {
                WordCom.ReleaseComObject(document);
            }
        }
    }
}
