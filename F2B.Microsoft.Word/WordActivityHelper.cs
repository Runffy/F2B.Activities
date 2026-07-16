using System;
using System.Activities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    internal static class WordActivityHelper
    {
        internal static bool IsBound(Argument argument)
        {
            return argument != null && argument.Expression != null;
        }

        internal static T GetOrDefault<T>(InArgument<T> argument, CodeActivityContext context, T fallback)
        {
            if (!IsBound(argument))
            {
                return fallback;
            }

            return argument.Get(context);
        }

        internal static string GetOptionalPath(InArgument<string> argument, CodeActivityContext context)
        {
            if (!IsBound(argument))
            {
                return null;
            }

            var value = argument.Get(context);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        internal static string RequireNonEmpty(InArgument<string> argument, CodeActivityContext context, string argumentName)
        {
            if (!IsBound(argument))
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

        internal static InteropWord.Document GetOptionalDocument(
            InOutArgument<InteropWord.Document> argument,
            CodeActivityContext context)
        {
            if (!IsBound(argument))
            {
                return null;
            }

            return argument.Get(context);
        }

        internal static void SetDocument(
            InOutArgument<InteropWord.Document> argument,
            CodeActivityContext context,
            InteropWord.Document document)
        {
            if (IsBound(argument))
            {
                argument.Set(context, document);
            }
        }

        internal static void SetDocument(
            OutArgument<InteropWord.Document> argument,
            CodeActivityContext context,
            InteropWord.Document document)
        {
            if (IsBound(argument))
            {
                argument.Set(context, document);
            }
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

        internal static int ParseRgbColor(string rgb)
        {
            if (string.IsNullOrWhiteSpace(rgb))
            {
                throw new ArgumentException("Rgb color is required when Color Mode is Rgb.");
            }

            var text = rgb.Trim();
            if (text.StartsWith("#", StringComparison.Ordinal))
            {
                text = text.Substring(1);
                if (text.Length != 6)
                {
                    throw new ArgumentException("RGB hex color must be in #RRGGBB format.");
                }

                var value = Convert.ToInt32(text, 16);
                var r = (value >> 16) & 0xFF;
                var g = (value >> 8) & 0xFF;
                var b = value & 0xFF;
                return r + (g << 8) + (b << 16);
            }

            var parts = text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                throw new ArgumentException("RGB color must be #RRGGBB or R,G,B.");
            }

            var red = ParseByte(parts[0], "R");
            var green = ParseByte(parts[1], "G");
            var blue = ParseByte(parts[2], "B");
            return red + (green << 8) + (blue << 16);
        }

        private static int ParseByte(string text, string name)
        {
            if (!int.TryParse(text.Trim(), out var value) || value < 0 || value > 255)
            {
                throw new ArgumentException(name + " must be an integer between 0 and 255.");
            }

            return value;
        }

        internal static InteropWord.WdColor ResolveNamedColor(string colorName)
        {
            if (string.IsNullOrWhiteSpace(colorName))
            {
                throw new ArgumentException("ColorName is required when Color Mode is Named.");
            }

            var name = colorName.Trim().Replace(" ", string.Empty);
            if (Enum.TryParse(name, ignoreCase: true, out InteropWord.WdColor color))
            {
                return color;
            }

            if (Enum.TryParse("wdColor" + name, ignoreCase: true, out color))
            {
                return color;
            }

            throw new ArgumentException("Unsupported Word color name: " + colorName);
        }
    }
}
