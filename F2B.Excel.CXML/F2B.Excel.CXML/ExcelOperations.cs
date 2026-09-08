using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace F2B.Excel.CXML
{
    /// <summary>
    /// Core ClosedXML operations shared by Excel activities.
    /// </summary>
    internal static class ExcelOperations
    {
        #region Workbook / sheets

        internal static IXLWorksheet GetWorksheet(ExcelWorkbook wb, string sheetNameOrNull)
        {
            EnsureWorkbook(wb);
            string name = string.IsNullOrWhiteSpace(sheetNameOrNull)
                ? wb.ActiveSheetName
                : sheetNameOrNull.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                name = wb.XlWorkbook.Worksheet(1).Name;
                wb.ActiveSheetName = name;
            }

            if (!wb.XlWorkbook.TryGetWorksheet(name, out IXLWorksheet ws) || ws == null)
            {
                throw new ArgumentException("Worksheet not found: " + name);
            }

            return ws;
        }

        internal static string ActivateSheet(ExcelWorkbook wb, string name, int? index0Based)
        {
            EnsureWorkbook(wb);

            IXLWorksheet ws;
            if (!string.IsNullOrWhiteSpace(name))
            {
                ws = GetWorksheet(wb, name.Trim());
            }
            else if (index0Based.HasValue)
            {
                int position1Based = index0Based.Value + 1;
                if (index0Based.Value < 0 || position1Based > wb.XlWorkbook.Worksheets.Count)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(index0Based),
                        "Sheet index is out of range: " + index0Based.Value);
                }

                ws = wb.XlWorkbook.Worksheet(position1Based);
            }
            else
            {
                throw new ArgumentException("Sheet Name or Index is required.");
            }

            wb.ActiveSheetName = ws.Name;
            return wb.ActiveSheetName;
        }

        internal static string[] GetAllSheetNames(ExcelWorkbook wb)
        {
            EnsureWorkbook(wb);
            return wb.XlWorkbook.Worksheets.Select(s => s.Name).ToArray();
        }

        internal static string GetActiveSheetName(ExcelWorkbook wb)
        {
            EnsureWorkbook(wb);
            if (string.IsNullOrWhiteSpace(wb.ActiveSheetName))
            {
                throw new InvalidOperationException("ActiveSheetName is empty.");
            }

            // Validate the pointer still exists.
            GetWorksheet(wb, wb.ActiveSheetName);
            return wb.ActiveSheetName;
        }

        internal static string NewSheet(ExcelWorkbook wb, string optionalName)
        {
            EnsureWorkbook(wb);

            string name = string.IsNullOrWhiteSpace(optionalName)
                ? GenerateNextSheetName(wb)
                : optionalName.Trim();

            if (wb.XlWorkbook.Worksheets.Any(s =>
                string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("Worksheet already exists: " + name);
            }

            IXLWorksheet ws = wb.XlWorkbook.Worksheets.Add(name);
            wb.ActiveSheetName = ws.Name;
            wb.IsDirty = true;
            return ws.Name;
        }

        internal static void DeleteSheet(ExcelWorkbook wb, string name, int? index0Based)
        {
            EnsureWorkbook(wb);

            IXLWorksheet target;
            if (!string.IsNullOrWhiteSpace(name))
            {
                target = GetWorksheet(wb, name.Trim());
            }
            else if (index0Based.HasValue)
            {
                int position1Based = index0Based.Value + 1;
                if (index0Based.Value < 0 || position1Based > wb.XlWorkbook.Worksheets.Count)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(index0Based),
                        "Sheet index is out of range: " + index0Based.Value);
                }

                target = wb.XlWorkbook.Worksheet(position1Based);
            }
            else
            {
                target = GetWorksheet(wb, null);
            }

            string deletedName = target.Name;
            bool deletedWasActive = string.Equals(
                deletedName,
                wb.ActiveSheetName,
                StringComparison.OrdinalIgnoreCase);
            // ClosedXML Position is 1-based.
            int deletedIndex0 = target.Position - 1;
            int sheetCount = wb.XlWorkbook.Worksheets.Count;

            if (sheetCount == 1)
            {
                // ClosedXML cannot leave a workbook with zero sheets: add replacement first.
                if (string.Equals(deletedName, "Sheet1", StringComparison.OrdinalIgnoreCase))
                {
                    target.Name = "__f2b_tmp_delete__";
                }

                IXLWorksheet replacement = wb.XlWorkbook.Worksheets.Add("Sheet1");
                target.Delete();
                wb.ActiveSheetName = replacement.Name;
                wb.IsDirty = true;
                return;
            }

            target.Delete();

            if (deletedWasActive)
            {
                // Previous by index; if deleted was first (0), activate next (now at 0).
                int activateIndex0 = deletedIndex0 > 0 ? deletedIndex0 - 1 : 0;
                int activatePosition1 = activateIndex0 + 1;
                if (activatePosition1 > wb.XlWorkbook.Worksheets.Count)
                {
                    activatePosition1 = 1;
                }

                wb.ActiveSheetName = wb.XlWorkbook.Worksheet(activatePosition1).Name;
            }
            else if (!wb.XlWorkbook.TryGetWorksheet(wb.ActiveSheetName, out _))
            {
                wb.ActiveSheetName = wb.XlWorkbook.Worksheet(1).Name;
            }

            wb.IsDirty = true;
        }

        internal static string CopySheet(ExcelWorkbook wb, string sourceNameOrNull, string newName)
        {
            EnsureWorkbook(wb);

            if (string.IsNullOrWhiteSpace(newName))
            {
                throw new ArgumentException("New sheet name is required.");
            }

            newName = newName.Trim();
            if (wb.XlWorkbook.Worksheets.Any(s =>
                string.Equals(s.Name, newName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("Worksheet already exists: " + newName);
            }

            IXLWorksheet source = GetWorksheet(wb, sourceNameOrNull);
            IXLWorksheet copy = source.CopyTo(newName);
            wb.ActiveSheetName = copy.Name;
            wb.IsDirty = true;
            return copy.Name;
        }

        internal static void Save(ExcelWorkbook wb)
        {
            EnsureWorkbook(wb);
            if (string.IsNullOrWhiteSpace(wb.FilePath))
            {
                throw new ArgumentException("FilePath is required to Save. Use SaveAs to specify a path.");
            }

            string path = ExcelActivityHelper.NormalizeExcelFilePath(wb.FilePath);
            try
            {
                wb.XlWorkbook.SaveAs(path);
                wb.FilePath = path;
                wb.IsDirty = false;
            }
            catch (IOException ex)
            {
                throw new IOException(
                    "Failed to save Excel file (it may be locked by another process such as Excel): " + path,
                    ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new IOException(
                    "Failed to save Excel file (access denied or file locked): " + path,
                    ex);
            }
        }

        internal static void SaveAs(ExcelWorkbook wb, string path, bool overwrite)
        {
            EnsureWorkbook(wb);
            path = ExcelActivityHelper.NormalizeExcelFilePath(path);

            if (File.Exists(path) && !overwrite)
            {
                throw new IOException("Target file already exists and Overwrite is false: " + path);
            }

            try
            {
                wb.XlWorkbook.SaveAs(path);
                wb.FilePath = path;
                wb.IsDirty = false;
            }
            catch (IOException ex)
            {
                throw new IOException(
                    "Failed to save Excel file (it may be locked by another process such as Excel): " + path,
                    ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new IOException(
                    "Failed to save Excel file (access denied or file locked): " + path,
                    ex);
            }
        }

        internal static void Close(ExcelWorkbook wb, bool saveChanges)
        {
            if (wb == null)
            {
                return;
            }

            try
            {
                if (saveChanges && wb.IsDirty)
                {
                    Save(wb);
                }
            }
            finally
            {
                wb.Dispose();
            }
        }

        #endregion

        #region Resolve / handles

        internal static string ResolveSheetName(
            ExcelWorkbook wb,
            string sheetNameOrNull,
            ExcelCell cellOrNull,
            ExcelRange rangeOrNull)
        {
            EnsureWorkbook(wb);

            if (!string.IsNullOrWhiteSpace(sheetNameOrNull))
            {
                return sheetNameOrNull.Trim();
            }

            if (cellOrNull != null && !string.IsNullOrWhiteSpace(cellOrNull.SheetName))
            {
                return cellOrNull.SheetName.Trim();
            }

            if (rangeOrNull != null && !string.IsNullOrWhiteSpace(rangeOrNull.SheetName))
            {
                return rangeOrNull.SheetName.Trim();
            }

            if (string.IsNullOrWhiteSpace(wb.ActiveSheetName))
            {
                throw new ArgumentException("Sheet name could not be resolved (ActiveSheetName is empty).");
            }

            return wb.ActiveSheetName;
        }

        /// <summary>
        /// Resolves workbook + sheet + cell address. ExcelCell wins when provided;
        /// workbook is taken from the cell when <paramref name="wb"/> is null.
        /// </summary>
        internal static void ResolveCellTarget(
            ExcelWorkbook wb,
            ExcelCell cellOrNull,
            string sheetNameOrNull,
            string addressOrNull,
            out ExcelWorkbook workbook,
            out string sheetName,
            out string address)
        {
            if (cellOrNull != null)
            {
                workbook = wb ?? cellOrNull.Workbook;
                EnsureWorkbook(workbook);

                sheetName = !string.IsNullOrWhiteSpace(cellOrNull.SheetName)
                    ? cellOrNull.SheetName.Trim()
                    : ResolveSheetName(workbook, sheetNameOrNull, null, null);

                if (!string.IsNullOrWhiteSpace(cellOrNull.Address))
                {
                    address = cellOrNull.Address.Trim();
                }
                else if (cellOrNull.RowIndex >= 1 && cellOrNull.ColumnIndex >= 1)
                {
                    address = ExcelAddressUtil.CellAddress(cellOrNull.RowIndex, cellOrNull.ColumnIndex);
                }
                else if (!string.IsNullOrWhiteSpace(addressOrNull))
                {
                    address = addressOrNull.Trim();
                }
                else
                {
                    throw new ArgumentException("Cell Address is required.");
                }

                return;
            }

            workbook = wb;
            EnsureWorkbook(workbook);

            if (string.IsNullOrWhiteSpace(addressOrNull))
            {
                throw new ArgumentException("Cell Address is required when ExcelCell is not provided.");
            }

            address = addressOrNull.Trim();
            sheetName = ResolveSheetName(workbook, sheetNameOrNull, null, null);
        }

        internal static ExcelCell CreateCellHandle(ExcelWorkbook wb, string sheet, string address)
        {
            EnsureWorkbook(wb);
            string sheetName = ResolveSheetName(wb, sheet, null, null);
            GetWorksheet(wb, sheetName);
            return ExcelAddressUtil.CreateCellFromAddress(wb, sheetName, address);
        }

        internal static ExcelRange CreateRangeHandle(ExcelWorkbook wb, string sheet, string address)
        {
            EnsureWorkbook(wb);
            string sheetName = ResolveSheetName(wb, sheet, null, null);
            GetWorksheet(wb, sheetName);
            return ExcelAddressUtil.CreateRangeFromAddress(wb, sheetName, address);
        }

        #endregion

        #region Cell read / write

        internal static object ReadCellValue(ExcelWorkbook wb, string sheet, string address)
        {
            EnsureWorkbook(wb);
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Cell address is required.");
            }

            IXLWorksheet ws = GetWorksheet(wb, sheet);
            IXLCell cell = ws.Cell(address.Trim());
            return ExtractCellObject(cell);
        }

        internal static void WriteCellValue(ExcelWorkbook wb, string sheet, string address, object value)
        {
            EnsureWorkbook(wb);
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Cell address is required.");
            }

            IXLWorksheet ws = GetWorksheet(wb, sheet);
            IXLCell cell = ws.Cell(address.Trim());
            AssignCellValueWithoutFormula(cell, value);
            wb.IsDirty = true;
        }

        internal static void SetFormula(ExcelWorkbook wb, string sheet, string address, string formula)
        {
            EnsureWorkbook(wb);
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Cell address is required.");
            }

            if (formula == null)
            {
                throw new ArgumentNullException(nameof(formula));
            }

            string f = formula.Trim();
            if (f.StartsWith("=", StringComparison.Ordinal))
            {
                f = f.Substring(1);
            }

            IXLWorksheet ws = GetWorksheet(wb, sheet);
            IXLCell cell = ws.Cell(address.Trim());
            cell.FormulaA1 = f;
            // Do not calculate.
            wb.IsDirty = true;
        }

        internal static T ConvertCellValueTo<T>(object value)
        {
            if (value is T direct)
            {
                return direct;
            }

            Type target = typeof(T);
            Type underlying = Nullable.GetUnderlyingType(target) ?? target;

            if (value == null)
            {
                return default(T);
            }

            if (underlying == typeof(bool))
            {
                if (value is double d)
                {
                    value = d > 0;
                }
                else if (value is float f)
                {
                    value = f > 0;
                }
                else if (value is decimal m)
                {
                    value = m > 0;
                }
                else if (value is int i)
                {
                    value = i > 0;
                }
                else if (value is long l)
                {
                    value = l > 0;
                }
                else if (value is string s)
                {
                    value = s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
                }
            }

            if (value != null && value.GetType() == typeof(double) && underlying == typeof(int))
            {
                value = int.Parse(value.ToString(), CultureInfo.InvariantCulture);
            }

            if (value != null && value.GetType() == typeof(DateTime) && underlying == typeof(string))
            {
                value = value.ToString();
            }

            if (value != null && value.GetType() == typeof(int) && underlying == typeof(string))
            {
                value = value.ToString();
            }

            if (value != null && value.GetType() == typeof(double) && underlying == typeof(string))
            {
                value = value.ToString();
            }

            if (value == null)
            {
                return default(T);
            }

            if (value is T typed)
            {
                return typed;
            }

            try
            {
                if (underlying.IsEnum && value is string enumText)
                {
                    return (T)Enum.Parse(underlying, enumText, ignoreCase: true);
                }

                object converted = Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
                if (target != underlying)
                {
                    return (T)converted;
                }

                return (T)converted;
            }
            catch (Exception ex)
            {
                throw new InvalidCastException(
                    "Cannot convert cell value of type '" + value.GetType().FullName
                    + "' to '" + typeof(T).FullName + "'.",
                    ex);
            }
        }

        #endregion

        #region Range DataTable

        internal static DataTable ReadRangeToDataTable(
            ExcelWorkbook wb,
            string sheet,
            string addressOrNull,
            bool hasHeaders)
        {
            EnsureWorkbook(wb);
            IXLWorksheet ws = GetWorksheet(wb, string.IsNullOrWhiteSpace(sheet) ? null : sheet);

            IXLRange used = ws.RangeUsed();
            IXLRange range;

            if (string.IsNullOrWhiteSpace(addressOrNull))
            {
                if (used == null)
                {
                    return new DataTable();
                }

                range = used;
            }
            else if (ExcelAddressUtil.IsSingleCellAddress(addressOrNull))
            {
                if (used == null)
                {
                    return new DataTable();
                }

                if (!ExcelAddressUtil.TryParseCell(addressOrNull, out int startRow, out int startCol))
                {
                    throw new ArgumentException("Invalid cell address: " + addressOrNull);
                }

                int usedFirstRow = used.FirstCell().Address.RowNumber;
                int usedFirstCol = used.FirstCell().Address.ColumnNumber;
                int usedLastRow = used.LastCell().Address.RowNumber;
                int usedLastCol = used.LastCell().Address.ColumnNumber;

                if (startRow < usedFirstRow || startRow > usedLastRow
                    || startCol < usedFirstCol || startCol > usedLastCol)
                {
                    return new DataTable();
                }

                range = ws.Range(startRow, startCol, usedLastRow, usedLastCol);
            }
            else
            {
                if (!ExcelAddressUtil.TryParseRange(
                    addressOrNull,
                    out int sr,
                    out int sc,
                    out int er,
                    out int ec))
                {
                    throw new ArgumentException("Invalid range address: " + addressOrNull);
                }

                range = ws.Range(sr, sc, er, ec);
            }

            return RangeToDataTable(range, hasHeaders);
        }

        internal static void WriteRangeFromDataTable(
            ExcelWorkbook wb,
            string sheet,
            string addressOrNull,
            DataTable table,
            bool hasHeaders)
        {
            EnsureWorkbook(wb);
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            IXLWorksheet ws = GetWorksheet(wb, string.IsNullOrWhiteSpace(sheet) ? null : sheet);

            int startRow = 1;
            int startCol = 1;
            bool clearFullRangeFirst = false;
            int clearEndRow = 0;
            int clearEndCol = 0;

            if (string.IsNullOrWhiteSpace(addressOrNull))
            {
                startRow = 1;
                startCol = 1;
            }
            else if (ExcelAddressUtil.IsSingleCellAddress(addressOrNull))
            {
                if (!ExcelAddressUtil.TryParseCell(addressOrNull, out startRow, out startCol))
                {
                    throw new ArgumentException("Invalid cell address: " + addressOrNull);
                }
            }
            else
            {
                if (!ExcelAddressUtil.TryParseRange(
                    addressOrNull,
                    out startRow,
                    out startCol,
                    out clearEndRow,
                    out clearEndCol))
                {
                    throw new ArgumentException("Invalid range address: " + addressOrNull);
                }

                clearFullRangeFirst = true;
            }

            if (clearFullRangeFirst)
            {
                IXLRange clearRange = ws.Range(startRow, startCol, clearEndRow, clearEndCol);
                clearRange.Clear(XLClearOptions.Contents | XLClearOptions.AllFormats);
            }

            int row = startRow;
            if (hasHeaders)
            {
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    AssignCellValueWithoutFormula(
                        ws.Cell(row, startCol + c),
                        table.Columns[c].ColumnName);
                }

                row++;
            }

            for (int r = 0; r < table.Rows.Count; r++)
            {
                DataRow dataRow = table.Rows[r];
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    object value = dataRow[c];
                    if (value == DBNull.Value)
                    {
                        value = null;
                    }

                    AssignCellValueWithoutFormula(ws.Cell(row + r, startCol + c), value);
                }
            }

            wb.IsDirty = true;
        }

        #endregion

        #region Clear / format / rich text

        internal static void Clear(
            ExcelWorkbook wb,
            string sheet,
            string address,
            ExcelClearOptions options,
            bool isCell)
        {
            EnsureWorkbook(wb);
            if (options == ExcelClearOptions.None)
            {
                return;
            }

            IXLWorksheet ws = GetWorksheet(wb, sheet);
            IXLCell singleCell = null;
            IXLRange range = null;

            if (string.IsNullOrWhiteSpace(address))
            {
                if (isCell)
                {
                    throw new ArgumentException("Address is required.");
                }

                range = ws.RangeUsed();
                if (range == null)
                {
                    return;
                }
            }
            else
            {
                address = address.Trim();
                if (isCell || ExcelAddressUtil.IsSingleCellAddress(address))
                {
                    if (!ExcelAddressUtil.TryParseCell(address, out _, out _))
                    {
                        throw new ArgumentException("Invalid cell address: " + address);
                    }

                    singleCell = ws.Cell(address);
                }
                else
                {
                    if (!ExcelAddressUtil.TryParseRange(address, out _, out _, out _, out _))
                    {
                        throw new ArgumentException("Invalid range address: " + address);
                    }

                    range = ws.Range(address);
                }
            }

            XLClearOptions xl = (XLClearOptions)0;
            if ((options & ExcelClearOptions.Value) != 0)
            {
                xl |= XLClearOptions.Contents;
            }

            if ((options & ExcelClearOptions.Format) != 0)
            {
                xl |= XLClearOptions.AllFormats;
            }

            if ((options & ExcelClearOptions.Comment) != 0)
            {
                xl |= XLClearOptions.Comments;
            }

            if (xl != (XLClearOptions)0)
            {
                if (singleCell != null)
                {
                    singleCell.Clear(xl);
                }
                else
                {
                    range.Clear(xl);
                }
            }

            if ((options & ExcelClearOptions.Hyperlink) != 0)
            {
                // ClosedXML 0.104 has no Hyperlinks flag on XLClearOptions.
                IEnumerable<IXLCell> cells = singleCell != null
                    ? (IEnumerable<IXLCell>)new[] { singleCell }
                    : range.Cells();

                foreach (IXLCell cell in cells)
                {
                    if (cell.HasHyperlink)
                    {
                        cell.SetHyperlink(null);
                    }
                }
            }

            wb.IsDirty = true;
        }

        internal static void SetBackgroundColor(
            ExcelWorkbook wb,
            string sheet,
            string address,
            Color color)
        {
            IXLCell cell = GetTargetCell(wb, sheet, address);
            cell.Style.Fill.PatternType = XLFillPatternValues.Solid;
            cell.Style.Fill.BackgroundColor = XLColor.FromColor(color);
            wb.IsDirty = true;
        }

        internal static void SetFontColor(
            ExcelWorkbook wb,
            string sheet,
            string address,
            Color color)
        {
            IXLCell cell = GetTargetCell(wb, sheet, address);
            cell.Style.Font.FontColor = XLColor.FromColor(color);
            wb.IsDirty = true;
        }

        internal static void SetFontFormat(
            ExcelWorkbook wb,
            string sheet,
            string address,
            string fontName,
            double? fontSize,
            bool? bold,
            bool? italic,
            bool? underline)
        {
            IXLCell cell = GetTargetCell(wb, sheet, address);
            IXLFont font = cell.Style.Font;

            if (!string.IsNullOrWhiteSpace(fontName))
            {
                font.FontName = fontName.Trim();
            }

            if (fontSize.HasValue)
            {
                font.FontSize = fontSize.Value;
            }

            if (bold.HasValue)
            {
                font.Bold = bold.Value;
            }

            if (italic.HasValue)
            {
                font.Italic = italic.Value;
            }

            if (underline.HasValue)
            {
                font.Underline = underline.Value
                    ? XLFontUnderlineValues.Single
                    : XLFontUnderlineValues.None;
            }

            wb.IsDirty = true;
        }

        internal static void SetRichText(
            ExcelWorkbook wb,
            string sheet,
            string address,
            IList<ExcelRichTextRun> runs,
            bool replace)
        {
            EnsureWorkbook(wb);
            if (runs == null)
            {
                throw new ArgumentNullException(nameof(runs));
            }

            IXLCell cell = GetTargetCell(wb, sheet, address);
            IXLRichText rich;
            if (replace || !cell.HasRichText)
            {
                rich = cell.CreateRichText();
            }
            else
            {
                rich = cell.GetRichText();
            }

            foreach (ExcelRichTextRun run in runs)
            {
                if (run == null)
                {
                    continue;
                }

                string text = run.Text ?? string.Empty;
                IXLRichString rs = rich.AddText(text);

                if (run.Bold.HasValue)
                {
                    rs.Bold = run.Bold.Value;
                }

                if (run.Italic.HasValue)
                {
                    rs.Italic = run.Italic.Value;
                }

                if (run.Underline.HasValue)
                {
                    rs.Underline = run.Underline.Value
                        ? XLFontUnderlineValues.Single
                        : XLFontUnderlineValues.None;
                }

                if (!string.IsNullOrWhiteSpace(run.FontName))
                {
                    rs.FontName = run.FontName.Trim();
                }

                if (run.FontSize.HasValue)
                {
                    rs.FontSize = run.FontSize.Value;
                }

                if (run.FontColor.HasValue)
                {
                    rs.FontColor = XLColor.FromColor(run.FontColor.Value);
                }
            }

            wb.IsDirty = true;
        }

        #endregion

        #region Helpers

        private static void EnsureWorkbook(ExcelWorkbook wb)
        {
            if (wb == null)
            {
                throw new ArgumentNullException(nameof(wb));
            }

            wb.EnsureNotDisposed();
        }

        private static string GenerateNextSheetName(ExcelWorkbook wb)
        {
            var existing = new HashSet<string>(
                wb.XlWorkbook.Worksheets.Select(s => s.Name),
                StringComparer.OrdinalIgnoreCase);

            for (int i = 1; i < int.MaxValue; i++)
            {
                string candidate = "Sheet" + i.ToString(CultureInfo.InvariantCulture);
                if (!existing.Contains(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Unable to generate a unique sheet name.");
        }

        private static IXLCell GetTargetCell(ExcelWorkbook wb, string sheet, string address)
        {
            EnsureWorkbook(wb);
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Cell address is required.");
            }

            IXLWorksheet ws = GetWorksheet(wb, sheet);
            return ws.Cell(address.Trim());
        }

        /// <summary>
        /// Writes a value as cell content. Strings (including those starting with '=') are never formulas.
        /// </summary>
        private static void AssignCellValueWithoutFormula(IXLCell cell, object value)
        {
            if (value == null || value == DBNull.Value)
            {
                cell.Clear(XLClearOptions.Contents);
                return;
            }

            // Explicit string path: never treat leading '=' as a formula.
            if (value is string s)
            {
                cell.Value = s;
                return;
            }

            if (value is DateTime dt)
            {
                cell.Value = dt;
                return;
            }

            if (value is DateTimeOffset dto)
            {
                cell.Value = dto.DateTime;
                return;
            }

            if (value is TimeSpan ts)
            {
                cell.Value = ts;
                return;
            }

            if (value is bool b)
            {
                cell.Value = b;
                return;
            }

            if (value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal)
            {
                cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return;
            }

            // Fallback: stringify so leading '=' in arbitrary objects cannot become formulas via FromObject quirks.
            cell.Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static object ExtractCellObject(IXLCell cell)
        {
            XLCellValue v = cell.Value;
            if (v.IsBlank)
            {
                return null;
            }

            if (v.IsBoolean)
            {
                return v.GetBoolean();
            }

            if (v.IsDateTime)
            {
                return v.GetDateTime();
            }

            if (v.IsTimeSpan)
            {
                return v.GetTimeSpan();
            }

            if (v.IsNumber)
            {
                double n = v.GetNumber();
                if (!double.IsNaN(n) && !double.IsInfinity(n)
                    && Math.Abs(n % 1) < double.Epsilon
                    && n >= int.MinValue && n <= int.MaxValue)
                {
                    return (int)n;
                }

                return n;
            }

            if (v.IsText)
            {
                return v.GetText();
            }

            if (v.IsError)
            {
                return v.ToString();
            }

            return v.ToString();
        }

        private static DataTable RangeToDataTable(IXLRange range, bool hasHeaders)
        {
            var table = new DataTable();
            if (range == null)
            {
                return table;
            }

            int rowCount = range.RowCount();
            int colCount = range.ColumnCount();
            if (rowCount <= 0 || colCount <= 0)
            {
                return table;
            }

            int dataStartRow = 1; // relative to range (1-based within range.Cell)

            if (hasHeaders)
            {
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 1; c <= colCount; c++)
                {
                    object headerObj = ExtractCellObject(range.Cell(1, c));
                    string header = headerObj == null
                        ? string.Empty
                        : Convert.ToString(headerObj, CultureInfo.InvariantCulture) ?? string.Empty;
                    table.Columns.Add(MakeUniqueColumnName(header, usedNames));
                }

                dataStartRow = 2;
            }
            else
            {
                for (int c = 1; c <= colCount; c++)
                {
                    table.Columns.Add("Column" + c.ToString(CultureInfo.InvariantCulture));
                }
            }

            for (int r = dataStartRow; r <= rowCount; r++)
            {
                DataRow row = table.NewRow();
                for (int c = 1; c <= colCount; c++)
                {
                    object value = ExtractCellObject(range.Cell(r, c));
                    row[c - 1] = value ?? DBNull.Value;
                }

                table.Rows.Add(row);
            }

            return table;
        }

        private static string MakeUniqueColumnName(string name, HashSet<string> used)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Column";
            }
            else
            {
                name = name.Trim();
            }

            string candidate = name;
            int i = 1;
            while (!used.Add(candidate))
            {
                candidate = name + "_" + i.ToString(CultureInfo.InvariantCulture);
                i++;
            }

            return candidate;
        }

        #endregion
    }
}
