using System;
using System.Activities;
using System.IO;

namespace F2B.Excel.CXML
{
    internal static class ExcelActivityHelper
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

        internal static string GetOptionalString(InArgument<string> argument, CodeActivityContext context)
        {
            if (!IsBound(argument))
            {
                return null;
            }

            string value = argument.Get(context);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        internal static string RequireNonEmpty(InArgument<string> argument, CodeActivityContext context, string name)
        {
            string value = GetOptionalString(argument, context);
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(name + " is required.");
            }

            return value;
        }

        internal static ExcelWorkbook GetOptionalWorkbook(
            InOutArgument<ExcelWorkbook> argument,
            CodeActivityContext context)
        {
            if (!IsBound(argument))
            {
                return null;
            }

            return argument.Get(context);
        }

        internal static void SetWorkbook(
            InOutArgument<ExcelWorkbook> argument,
            CodeActivityContext context,
            ExcelWorkbook workbook)
        {
            if (IsBound(argument))
            {
                argument.Set(context, workbook);
            }
        }

        internal static void SetIfBound<T>(OutArgument<T> argument, CodeActivityContext context, T value)
        {
            if (IsBound(argument))
            {
                argument.Set(context, value);
            }
        }

        internal static string NormalizeExcelFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("FilePath is required.");
            }

            path = path.Trim().Trim('"');
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext))
            {
                path += ".xlsx";
                ext = ".xlsx";
            }

            if (!string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(ext, ".xlsm", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Only .xlsx and .xlsm files are supported: " + path);
            }

            return path;
        }
    }
}
