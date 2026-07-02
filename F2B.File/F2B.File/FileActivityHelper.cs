using System;
using System.Activities;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace F2B.File
{
    internal static class FileActivityHelper
    {
        internal static T GetOrDefault<T>(InArgument<T> argument, CodeActivityContext context, T fallback)
        {
            if (argument == null || argument.Expression == null)
            {
                return fallback;
            }

            return argument.Get(context);
        }

        internal static string RequirePath(InArgument<string> argument, CodeActivityContext context, string argumentName)
        {
            if (argument == null || argument.Expression == null)
            {
                throw new ArgumentException(argumentName + " is required.");
            }

            var path = argument.Get(context);
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(argumentName + " is required.");
            }

            return path.Trim();
        }

        internal static string[] RequirePaths(InArgument<string[]> argument, CodeActivityContext context, string argumentName)
        {
            if (argument == null || argument.Expression == null)
            {
                throw new ArgumentException(argumentName + " is required.");
            }

            var paths = argument.Get(context);
            if (paths == null || paths.Length == 0)
            {
                throw new ArgumentException(argumentName + " is required.");
            }

            var normalized = paths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .ToArray();

            if (normalized.Length == 0)
            {
                throw new ArgumentException(argumentName + " is required.");
            }

            return normalized;
        }

        internal static void EnsureParentDirectoryExists(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        internal static void AddPathToZipArchive(ZipArchive archive, string sourcePath)
        {
            if (System.IO.File.Exists(sourcePath))
            {
                archive.CreateEntryFromFile(sourcePath, Path.GetFileName(sourcePath));
                return;
            }

            if (Directory.Exists(sourcePath))
            {
                var entryPrefix = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                AddDirectoryToZipArchive(archive, sourcePath, entryPrefix);
                return;
            }

            throw new FileNotFoundException("Source path was not found.", sourcePath);
        }

        private static void AddDirectoryToZipArchive(ZipArchive archive, string directoryPath, string entryPrefix)
        {
            foreach (var file in Directory.GetFiles(directoryPath))
            {
                var entryName = string.IsNullOrEmpty(entryPrefix)
                    ? Path.GetFileName(file)
                    : entryPrefix + "/" + Path.GetFileName(file);
                archive.CreateEntryFromFile(file, entryName.Replace('\\', '/'));
            }

            foreach (var directory in Directory.GetDirectories(directoryPath))
            {
                var directoryName = Path.GetFileName(directory);
                var nestedPrefix = string.IsNullOrEmpty(entryPrefix)
                    ? directoryName
                    : entryPrefix + "/" + directoryName;
                AddDirectoryToZipArchive(archive, directory, nestedPrefix);
            }
        }

        internal static bool MatchesNameFilter(string name, string regexPattern)
        {
            if (string.IsNullOrWhiteSpace(regexPattern))
                return true;

            return Regex.IsMatch(name ?? string.Empty, regexPattern.Trim(), RegexOptions.IgnoreCase);
        }

        internal static string[] ListFilteredFiles(string folderPath, string regexPattern)
        {
            return Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => MatchesNameFilter(Path.GetFileName(path), regexPattern))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static string[] ListFilteredFolders(string folderPath, string regexPattern)
        {
            return Directory.GetDirectories(folderPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => MatchesNameFilter(Path.GetFileName(path), regexPattern))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static string[] ListFilteredItems(string folderPath, string regexPattern)
        {
            return Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => MatchesNameFilter(Path.GetFileName(path), regexPattern))
                .Concat(Directory.GetDirectories(folderPath, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => MatchesNameFilter(Path.GetFileName(path), regexPattern)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static Encoding ResolveEncoding(string encodingName)
        {
            if (string.IsNullOrWhiteSpace(encodingName))
            {
                return Encoding.UTF8;
            }

            var normalized = encodingName.Trim();
            if (normalized.Equals("UTF-8", StringComparison.OrdinalIgnoreCase))
            {
                return new UTF8Encoding(false);
            }

            if (normalized.Equals("UTF-8 BOM", StringComparison.OrdinalIgnoreCase))
            {
                return new UTF8Encoding(true);
            }

            return Encoding.GetEncoding(normalized);
        }

        internal static FileShare ResolveFileShare(TextFileShareMode shareMode)
        {
            switch (shareMode)
            {
                case TextFileShareMode.None:
                    return FileShare.None;
                case TextFileShareMode.Read:
                    return FileShare.Read;
                case TextFileShareMode.Write:
                    return FileShare.Write;
                case TextFileShareMode.ReadWrite:
                    return FileShare.ReadWrite;
                case TextFileShareMode.Delete:
                    return FileShare.Delete;
                default:
                    return FileShare.ReadWrite;
            }
        }

        internal static string ReadAllText(string filePath, Encoding encoding, FileShare share)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, share))
            using (var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true))
            {
                return reader.ReadToEnd();
            }
        }

        internal static void WriteAllText(string filePath, string content, Encoding encoding, FileShare share, bool append)
        {
            if (append)
            {
                using (var stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, share))
                {
                    stream.Seek(0, SeekOrigin.End);
                    using (var writer = new StreamWriter(stream, encoding))
                    {
                        writer.Write(content);
                    }
                }

                return;
            }

            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, share))
            using (var writer = new StreamWriter(stream, encoding))
            {
                writer.Write(content);
            }
        }

        internal static void CopyDirectoryRecursive(string sourceDir, string destinationDir, bool overwrite)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(destinationDir, fileName);
                System.IO.File.Copy(file, destFile, overwrite);
            }

            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                var directoryName = Path.GetFileName(directory);
                var destDirectory = Path.Combine(destinationDir, directoryName);
                CopyDirectoryRecursive(directory, destDirectory, overwrite);
            }
        }

        internal static void DeleteDirectoryRecursive(string directoryPath)
        {
            foreach (var file in Directory.GetFiles(directoryPath))
            {
                System.IO.File.SetAttributes(file, FileAttributes.Normal);
                System.IO.File.Delete(file);
            }

            foreach (var directory in Directory.GetDirectories(directoryPath))
            {
                DeleteDirectoryRecursive(directory);
            }

            Directory.Delete(directoryPath, false);
        }

        internal static long GetDirectorySize(string directoryPath)
        {
            long size = 0;

            foreach (var file in Directory.GetFiles(directoryPath))
            {
                size += new FileInfo(file).Length;
            }

            foreach (var directory in Directory.GetDirectories(directoryPath))
            {
                size += GetDirectorySize(directory);
            }

            return size;
        }
    }
}
