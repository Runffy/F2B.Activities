namespace F2B.Excel.CXML
{
    /// <summary>
    /// Lightweight cell handle carrying workbook, sheet, address and 1-based indices.
    /// </summary>
    public sealed class ExcelCell
    {
        public ExcelWorkbook Workbook { get; set; }

        public string SheetName { get; set; }

        public string Address { get; set; }

        /// <summary>1-based row index.</summary>
        public int RowIndex { get; set; }

        /// <summary>1-based column index.</summary>
        public int ColumnIndex { get; set; }
    }
}
