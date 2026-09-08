namespace F2B.Excel.CXML
{
    /// <summary>
    /// Lightweight range handle carrying workbook, sheet, address and 1-based indices.
    /// </summary>
    public sealed class ExcelRange
    {
        public ExcelWorkbook Workbook { get; set; }

        public string SheetName { get; set; }

        public string Address { get; set; }

        /// <summary>1-based.</summary>
        public int StartRowIndex { get; set; }

        /// <summary>1-based.</summary>
        public int EndRowIndex { get; set; }

        /// <summary>1-based.</summary>
        public int StartColumnIndex { get; set; }

        /// <summary>1-based.</summary>
        public int EndColumnIndex { get; set; }
    }
}
