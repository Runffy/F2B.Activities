using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.Threading;
using System.Windows.Forms;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Browse For Folder")]
    [Description("Show a folder browser dialog and output the selected folder path.")]
    public sealed class BrowseForFolderActivity : CodeActivity
    {
        public BrowseForFolderActivity()
        {
            DisplayName = "Browse For Folder";
        }

        [DisplayName("Description")]
        [Description("Description text shown in the folder browser dialog.")]
        [Category("Input.A")]
        [DefaultValue("Select Folder")]
        public InArgument<string> Description { get; set; } = "Select Folder";

        [DisplayName("Selected Path")]
        [Description("Selected folder path, or empty when cancelled.")]
        [Category("Output")]
        public OutArgument<string> SelectedPath { get; set; }

        [DisplayName("Cancelled")]
        [Description("True when the dialog was cancelled.")]
        [Category("Output")]
        public OutArgument<bool> Cancelled { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var description = FileActivityHelper.GetOrDefault(Description, context, "Select Folder");

            string selectedPath = null;
            var cancelled = true;

            var thread = new Thread(() =>
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = description;
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        selectedPath = dialog.SelectedPath;
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
