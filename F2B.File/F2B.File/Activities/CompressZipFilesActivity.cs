using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;

namespace F2B.File
{
    [Designer(typeof(FileRequiredFieldsActivityDesigner))]
    [DisplayName("Compress Zip Files")]
    [Description("Compress one or more files or folders into a zip archive.")]
    public sealed class CompressZipFilesActivity : CodeActivity
    {
        public CompressZipFilesActivity()
        {
            DisplayName = "Compress Zip Files";
        }

        [DisplayName("Source Paths")]
        [Description("One or more file or folder paths to compress.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string[]> SourcePaths { get; set; }

        [DisplayName("Archive Path")]
        [Description("Destination zip archive path.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> ArchivePath { get; set; }

        [DisplayName("Overwrite")]
        [Description("Overwrite the archive if it already exists.")]
        [Category("Input.C")]
        [DefaultValue(true)]
        public InArgument<bool> Overwrite { get; set; } = true;

        protected override void Execute(CodeActivityContext context)
        {
            var sourcePaths = FileActivityHelper.RequirePaths(SourcePaths, context, nameof(SourcePaths));
            var archivePath = FileActivityHelper.RequirePath(ArchivePath, context, nameof(ArchivePath));
            var overwrite = FileActivityHelper.GetOrDefault(Overwrite, context, true);

            FileActivityHelper.EnsureParentDirectoryExists(archivePath);

            if (System.IO.File.Exists(archivePath))
            {
                if (!overwrite)
                {
                    throw new IOException("Archive already exists: " + archivePath);
                }

                System.IO.File.Delete(archivePath);
            }

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                foreach (var sourcePath in sourcePaths)
                {
                    FileActivityHelper.AddPathToZipArchive(archive, sourcePath);
                }
            }
        }
    }
}
