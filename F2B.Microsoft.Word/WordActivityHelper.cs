using System;
using System.Activities;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace F2B.Microsoft.Word
{
    internal static class WordActivityHelper
    {
        internal static T GetOrDefault<T>(InArgument<T> argument, CodeActivityContext context, T fallback)
        {
            if (argument == null || argument.Expression == null)
            {
                return fallback;
            }

            return argument.Get(context);
        }

        internal static string RequireNonEmpty(InArgument<string> argument, CodeActivityContext context, string argumentName)
        {
            if (argument == null || argument.Expression == null)
            {
                throw new ArgumentException(argumentName + " is required.");
            }

            var value = argument.Get(context);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(argumentName + " is required.");
            }

            return value.Trim();
        }

        internal static string NormalizeWordFilePath(string wordFilePath)
        {
            if (string.IsNullOrWhiteSpace(wordFilePath))
            {
                throw new ArgumentException("WordFilePath is required.");
            }

            var path = wordFilePath.Trim();
            if (!Path.HasExtension(path))
            {
                path += ".docx";
            }

            return Path.GetFullPath(path);
        }

        internal static IReadOnlyList<string> ParseAndValidateImagePaths(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                throw new ArgumentException("ImagePath is required.");
            }

            var paths = imagePath
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .Select(Path.GetFullPath)
                .ToList();

            if (paths.Count == 0)
            {
                throw new ArgumentException("ImagePath is required.");
            }

            var missing = paths.Where(path => !File.Exists(path)).ToList();
            if (missing.Count > 0)
            {
                throw new FileNotFoundException(
                    "One or more image files were not found: " + string.Join("; ", missing));
            }

            return paths;
        }

        internal static float ToPoints(double value, WordImageUnit unit)
        {
            switch (unit)
            {
                case WordImageUnit.Cm:
                    return (float)(value * 72d / 2.54d);
                case WordImageUnit.Mm:
                    return (float)(value * 72d / 25.4d);
                case WordImageUnit.Inch:
                    return (float)(value * 72d);
                case WordImageUnit.Px:
                    return (float)(value * 72d / 96d);
                case WordImageUnit.Pt:
                    return (float)value;
                default:
                    throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported image unit.");
            }
        }

        internal static void EnsureParentDirectoryExists(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
