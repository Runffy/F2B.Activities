using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Extract Unzip Files")]
    [Description("Extract files from a zip archive to a destination folder.")]
    public sealed class ExtractUnzipFilesActivity : CodeActivity
    {
        public ExtractUnzipFilesActivity()
        {
            DisplayName = "Extract Unzip Files";
        }

        [DisplayName("Archive Path")]
        [Description("Path to the zip archive.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> ArchivePath { get; set; }

        [DisplayName("Destination Path")]
        [Description("Folder where files will be extracted.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> DestinationPath { get; set; }

        [DisplayName("Overwrite")]
        [Description("Overwrite existing files in the destination folder.")]
        [Category("Input.C")]
        [DefaultValue(true)]
        public InArgument<bool> Overwrite { get; set; } = true;

        protected override void Execute(CodeActivityContext context)
        {
            var archivePath = FileActivityHelper.RequirePath(ArchivePath, context, nameof(ArchivePath));
            var destinationPath = FileActivityHelper.RequirePath(DestinationPath, context, nameof(DestinationPath));
            var overwrite = FileActivityHelper.GetOrDefault(Overwrite, context, true);

            if (!System.IO.File.Exists(archivePath))
            {
                throw new FileNotFoundException("Archive was not found.", archivePath);
            }

            Directory.CreateDirectory(destinationPath);

            if (overwrite)
            {
                ZipFile.ExtractToDirectory(archivePath, destinationPath);
                return;
            }

            using (var archive = ZipFile.OpenRead(archivePath))
            {
                foreach (var entry in archive.Entries)
                {
                    var destinationFile = System.IO.Path.Combine(destinationPath, entry.FullName);
                    var destinationDirectory = System.IO.Path.GetDirectoryName(destinationFile);
                    if (!string.IsNullOrEmpty(destinationDirectory))
                    {
                        Directory.CreateDirectory(destinationDirectory);
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    if (System.IO.File.Exists(destinationFile))
                    {
                        throw new IOException("Destination file already exists: " + destinationFile);
                    }

                    entry.ExtractToFile(destinationFile, false);
                }
            }
        }
    }
}
