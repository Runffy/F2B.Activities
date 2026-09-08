using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace F2B.Excel.CXML
{
    internal static class ExcelAddressUtil
    {
        private static readonly Regex SingleCellRegex = new Regex(
            @"^\s*\$?([A-Za-z]+)\$?(\d+)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex RangeRegex = new Regex(
            @"^\s*\$?([A-Za-z]+)\$?(\d+)\s*:\s*\$?([A-Za-z]+)\$?(\d+)\s*$",
            RegexOptions.Compiled);

        internal static string ColumnIndexToLetters(int columnIndex1Based)
        {
            if (columnIndex1Based < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(columnIndex1Based), "Column index must be 1-based.");
            }

            var sb = new StringBuilder();
            int n = columnIndex1Based;
            while (n > 0)
            {
                n--;
                sb.Insert(0, (char)('A' + (n % 26)));
                n /= 26;
            }

            return sb.ToString();
        }

        internal static int ColumnLettersToIndex(string letters)
        {
            if (string.IsNullOrWhiteSpace(letters))
            {
                throw new ArgumentException("Column letters are required.");
            }

            int result = 0;
            foreach (char c in letters.Trim().ToUpperInvariant())
            {
                if (c < 'A' || c > 'Z')
                {
                    throw new ArgumentException("Invalid column letters: " + letters);
                }

                result = result * 26 + (c - 'A' + 1);
            }

            return result;
        }

        internal static string CellAddress(int rowIndex1Based, int columnIndex1Based)
        {
            if (rowIndex1Based < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(rowIndex1Based), "Row index must be 1-based.");
            }

            return ColumnIndexToLetters(columnIndex1Based) + rowIndex1Based.ToString(CultureInfo.InvariantCulture);
        }

        internal static string RangeAddress(
            int startRow1Based,
            int startCol1Based,
            int endRow1Based,
            int endCol1Based)
        {
            return CellAddress(startRow1Based, startCol1Based) + ":" + CellAddress(endRow1Based, endCol1Based);
        }

        internal static bool TryParseCell(string address, out int row1Based, out int col1Based)
        {
            row1Based = 0;
            col1Based = 0;
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            Match m = SingleCellRegex.Match(address);
            if (!m.Success)
            {
                return false;
            }

            col1Based = ColumnLettersToIndex(m.Groups[1].Value);
            row1Based = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            return row1Based >= 1 && col1Based >= 1;
        }

        internal static bool TryParseRange(
            string address,
            out int startRow,
            out int startCol,
            out int endRow,
            out int endCol)
        {
            startRow = startCol = endRow = endCol = 0;
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            Match range = RangeRegex.Match(address);
            if (range.Success)
            {
                startCol = ColumnLettersToIndex(range.Groups[1].Value);
                startRow = int.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture);
                endCol = ColumnLettersToIndex(range.Groups[3].Value);
                endRow = int.Parse(range.Groups[4].Value, CultureInfo.InvariantCulture);
                Normalize(ref startRow, ref endRow);
                Normalize(ref startCol, ref endCol);
                return true;
            }

            if (TryParseCell(address, out startRow, out startCol))
            {
                endRow = startRow;
                endCol = startCol;
                return true;
            }

            return false;
        }

        internal static bool IsSingleCellAddress(string address)
        {
            return !string.IsNullOrWhiteSpace(address)
                && SingleCellRegex.IsMatch(address)
                && !address.Contains(":");
        }

        internal static ExcelCell CreateCell(ExcelWorkbook workbook, string sheetName, int row1Based, int col1Based)
        {
            string address = CellAddress(row1Based, col1Based);
            return new ExcelCell
            {
                Workbook = workbook,
                SheetName = sheetName,
                Address = address,
                RowIndex = row1Based,
                ColumnIndex = col1Based
            };
        }

        internal static ExcelRange CreateRange(
            ExcelWorkbook workbook,
            string sheetName,
            int startRow,
            int startCol,
            int endRow,
            int endCol)
        {
            Normalize(ref startRow, ref endRow);
            Normalize(ref startCol, ref endCol);
            return new ExcelRange
            {
                Workbook = workbook,
                SheetName = sheetName,
                Address = RangeAddress(startRow, startCol, endRow, endCol),
                StartRowIndex = startRow,
                EndRowIndex = endRow,
                StartColumnIndex = startCol,
                EndColumnIndex = endCol
            };
        }

        internal static ExcelCell CreateCellFromAddress(ExcelWorkbook workbook, string sheetName, string address)
        {
            if (!TryParseCell(address, out int row, out int col))
            {
                throw new ArgumentException("Invalid cell address: " + address);
            }

            return CreateCell(workbook, sheetName, row, col);
        }

        internal static ExcelRange CreateRangeFromAddress(ExcelWorkbook workbook, string sheetName, string address)
        {
            if (!TryParseRange(address, out int sr, out int sc, out int er, out int ec))
            {
                throw new ArgumentException("Invalid range address: " + address);
            }

            return CreateRange(workbook, sheetName, sr, sc, er, ec);
        }

        private static void Normalize(ref int a, ref int b)
        {
            if (a > b)
            {
                int t = a;
                a = b;
                b = t;
            }
        }
    }
}
