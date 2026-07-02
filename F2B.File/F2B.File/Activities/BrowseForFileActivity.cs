using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Browse For File")]
    [Description("Show an Open File dialog and output the selected file path.")]
    public sealed class BrowseForFileActivity : CodeActivity
    {
        public BrowseForFileActivity()
        {
            DisplayName = "Browse For File";
        }

        [DisplayName("Title")]
        [Description("Dialog title text.")]
        [Category("Input.A")]
        [DefaultValue("Select File")]
        public InArgument<string> Title { get; set; } = "Select File";

        [DisplayName("Filter")]
        [Description("File filter, for example Text files (*.txt)|*.txt|All files (*.*)|*.*")]
        [Category("Input.B")]
        [DefaultValue("All files (*.*)|*.*")]
        public InArgument<string> Filter { get; set; } = "All files (*.*)|*.*";

        [DisplayName("Initial Directory")]
        [Description("Initial folder shown by the dialog.")]
        [Category("Input.C")]
        public InArgument<string> InitialDirectory { get; set; }

        [DisplayName("Selected Path")]
        [Description("Selected file path, or empty when cancelled.")]
        [Category("Output")]
        public OutArgument<string> SelectedPath { get; set; }

        [DisplayName("Cancelled")]
        [Description("True when the dialog was cancelled.")]
        [Category("Output")]
        public OutArgument<bool> Cancelled { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var title = FileActivityHelper.GetOrDefault(Title, context, "Select File");
            var filter = FileActivityHelper.GetOrDefault(Filter, context, "All files (*.*)|*.*");
            var initialDirectory = FileActivityHelper.GetOrDefault(InitialDirectory, context, string.Empty);

            string selectedPath = null;
            var cancelled = true;

            var thread = new Thread(() =>
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = title;
                    dialog.Filter = filter;
                    if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
                    {
                        dialog.InitialDirectory = initialDirectory;
                    }

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        selectedPath = dialog.FileName;
                        cancelled = false;
                    }
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            SelectedPath?.Set(context, selectedPath ?? string.Empty);
            Cancelled?.Set(context, cancelled);
        }
    }
}
