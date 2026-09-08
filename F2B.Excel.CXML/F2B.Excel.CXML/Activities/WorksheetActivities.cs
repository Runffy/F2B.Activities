using System;
using System.Activities;
using System.ComponentModel;
using System.Data;

namespace F2B.Excel.CXML
{
    [DisplayName("Worksheet-ReadRange")]
    [Description("Read a range into DataTable. Sheet empty=Active; Address empty=UsedRange; single cell expands to UsedRange bottom-right.")]
    public sealed class WorksheetReadRangeActivity : ExcelWorkbookActivity
    {
        public WorksheetReadRangeActivity()
        {
            DisplayName = "Worksheet-ReadRange";
            HasHeaders = true;
        }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Address")]
        [Category("Input.B")]
        [Description("A1 or A1:D10. Single cell = start; expands to UsedRange. Empty = UsedRange.")]
        public InArgument<string> Address { get; set; }

        [DisplayName("Has Headers")]
        [Category("Input.B")]
        [DefaultValue(true)]
        public InArgument<bool> HasHeaders { get; set; }

        [DisplayName("Data Table")]
        [Category("Output")]
        public OutArgument<DataTable> DataTable { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ExcelWorkbook wb = ResolveWorkbook(context);
            string sheet = ExcelActivityHelper.GetOptionalString(SheetName, context);
            string address = ExcelActivityHelper.GetOptionalString(Address, context);
            bool headers = ExcelActivityHelper.GetOrDefault(HasHeaders, context, true);
            DataTable table = ExcelOperations.ReadRangeToDataTable(wb, sheet, address, headers);
            ExcelActivityHelper.SetIfBound(DataTable, context, table);
            ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
        }
    }

    [DisplayName("Worksheet-WriteRange")]
    [Description("Write DataTable. Sheet empty=Active; Address empty=from A1; full range clears target first.")]
    public sealed class WorksheetWriteRangeActivity : ExcelWorkbookActivity
    {
        public WorksheetWriteRangeActivity()
        {
            DisplayName = "Worksheet-WriteRange";
            HasHeaders = true;
        }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Address")]
        [Category("Input.B")]
        public InArgument<string> Address { get; set; }

        [DisplayName("Data Table")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<DataTable> DataTable { get; set; }

        [DisplayName("Has Headers")]
        [Category("Input.B")]
        [DefaultValue(true)]
        public InArgument<bool> HasHeaders { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ExcelWorkbook wb = ResolveWorkbook(context);
            string sheet = ExcelActivityHelper.GetOptionalString(SheetName, context);
            string address = ExcelActivityHelper.GetOptionalString(Address, context);
            DataTable table = DataTable.Get(context);
            bool headers = ExcelActivityHelper.GetOrDefault(HasHeaders, context, true);
            ExcelOperations.WriteRangeFromDataTable(wb, sheet, address, table, headers);
            ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
        }
    }

    [DisplayName("Worksheet-ReadCell")]
    [Designer(typeof(ExcelMinimalActivityDesigner))]
    [System.Activities.Presentation.DefaultTypeArgument(typeof(string))]
    public sealed class WorksheetReadCellActivity<TResult> : NativeActivity<TResult>
    {
        public WorksheetReadCellActivity()
        {
            DisplayName = "Worksheet-ReadCell";
        }

        [DisplayName("Workbook")]
        [Category("Input.A")]
        public InOutArgument<ExcelWorkbook> Workbook { get; set; }

        [DisplayName("File Path")]
        [Category("Input.A")]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Address")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> Address { get; set; }

        protected override void Execute(NativeActivityContext context)
        {
            ExcelWorkbook existing = Workbook != null && Workbook.Expression != null ? Workbook.Get(context) : null;
            string path = FilePath != null && FilePath.Expression != null ? FilePath.Get(context) : null;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = null;
            }
            else
            {
                path = path.Trim();
            }

            using (ExcelWorkbookSession session = ExcelWorkbookSession.Acquire(existing, path))
            {
                ExcelWorkbook wb = session.Workbook;
                if (Workbook != null && Workbook.Expression != null)
                {
                    Workbook.Set(context, wb);
                }

                string sheet = SheetName != null && SheetName.Expression != null ? SheetName.Get(context) : null;
                if (string.IsNullOrWhiteSpace(sheet))
                {
                    sheet = null;
                }

                string address = Address.Get(context);
                object raw = ExcelOperations.ReadCellValue(wb, sheet, address);
                Result.Set(context, ExcelOperations.ConvertCellValueTo<TResult>(raw));
            }
        }
    }

    [DisplayName("Worksheet-WriteCell")]
    [Designer(typeof(ExcelMinimalActivityDesigner))]
    [System.Activities.Presentation.DefaultTypeArgument(typeof(string))]
    public sealed class WorksheetWriteCellActivity<TValue> : NativeActivity
    {
        public WorksheetWriteCellActivity()
        {
            DisplayName = "Worksheet-WriteCell";
        }

        [DisplayName("Workbook")]
        [Category("Input.A")]
        public InOutArgument<ExcelWorkbook> Workbook { get; set; }

        [DisplayName("File Path")]
        [Category("Input.A")]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Address")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> Address { get; set; }

        [DisplayName("Value")]
        [Category("Input.B")]
        public InArgument<TValue> Value { get; set; }

        protected override void Execute(NativeActivityContext context)
        {
            ExcelWorkbook existing = Workbook != null && Workbook.Expression != null ? Workbook.Get(context) : null;
            string path = FilePath != null && FilePath.Expression != null ? FilePath.Get(context) : null;
            path = string.IsNullOrWhiteSpace(path) ? null : path.Trim();

            using (ExcelWorkbookSession session = ExcelWorkbookSession.Acquire(existing, path))
            {
                ExcelWorkbook wb = session.Workbook;
                if (Workbook != null && Workbook.Expression != null)
                {
                    Workbook.Set(context, wb);
                }

                string sheet = SheetName != null && SheetName.Expression != null ? SheetName.Get(context) : null;
                sheet = string.IsNullOrWhiteSpace(sheet) ? null : sheet.Trim();
                string address = Address.Get(context);
                TValue value = Value != null && Value.Expression != null ? Value.Get(context) : default(TValue);
                ExcelOperations.WriteCellValue(wb, sheet, address, value);
            }
        }
    }

    [DisplayName("Worksheet-GetCellByAddress")]
    public sealed class WorksheetGetCellByAddressActivity : ExcelWorkbookActivity
    {
        public WorksheetGetCellByAddressActivity()
        {
            DisplayName = "Worksheet-GetCellByAddress";
        }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Address")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> Address { get; set; }

        [DisplayName("Cell")]
        [Category("Output")]
        public OutArgument<ExcelCell> Cell { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ExcelWorkbook wb = ResolveWorkbook(context);
            string sheet = ExcelActivityHelper.GetOptionalString(SheetName, context);
            string address = ExcelActivityHelper.RequireNonEmpty(Address, context, nameof(Address));
            ExcelCell cell = ExcelOperations.CreateCellHandle(wb, sheet, address);
            ExcelActivityHelper.SetIfBound(Cell, context, cell);
            ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
        }
    }

    [DisplayName("Worksheet-GetRangeByAddress")]
    public sealed class WorksheetGetRangeByAddressActivity : ExcelWorkbookActivity
    {
        public WorksheetGetRangeByAddressActivity()
        {
            DisplayName = "Worksheet-GetRangeByAddress";
        }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Address")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> Address { get; set; }

        [DisplayName("Range")]
        [Category("Output")]
        public OutArgument<ExcelRange> Range { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ExcelWorkbook wb = ResolveWorkbook(context);
            string sheet = ExcelActivityHelper.GetOptionalString(SheetName, context);
            string address = ExcelActivityHelper.RequireNonEmpty(Address, context, nameof(Address));
            ExcelRange range = ExcelOperations.CreateRangeHandle(wb, sheet, address);
            ExcelActivityHelper.SetIfBound(Range, context, range);
            ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
        }
    }

    [DisplayName("Range-Clear")]
    public sealed class RangeClearActivity : ExcelWorkbookActivity
    {
        public RangeClearActivity()
        {
            DisplayName = "Range-Clear";
            ClearOptions = ExcelClearOptions.All;
        }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Address")]
        [Category("Input.B")]
        [Description("Empty = UsedRange of the sheet.")]
        public InArgument<string> Address { get; set; }

        [DisplayName("Range")]
        [Category("Input.B")]
        public InArgument<ExcelRange> Range { get; set; }

        [DisplayName("Clear Options")]
        [Category("Input.C")]
        [DefaultValue(ExcelClearOptions.All)]
        public ExcelClearOptions ClearOptions { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ExcelWorkbook wb = ResolveWorkbook(context);
            ExcelRange range = ExcelActivityHelper.IsBound(Range) ? Range.Get(context) : null;
            string sheet = ExcelOperations.ResolveSheetName(
                wb,
                ExcelActivityHelper.GetOptionalString(SheetName, context),
                null,
                range);
            string address = ExcelActivityHelper.GetOptionalString(Address, context);
            if (string.IsNullOrEmpty(address) && range != null)
            {
                address = range.Address;
                if (range.Workbook != null)
                {
                    wb = range.Workbook;
                }
            }

            ExcelOperations.Clear(wb, sheet, address, ClearOptions, isCell: false);
            ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
        }
    }

    [DisplayName("Cell-Clear")]
    public sealed class CellClearActivity : ExcelWorkbookActivity
    {
        public CellClearActivity()
        {
            DisplayName = "Cell-Clear";
            ClearOptions = ExcelClearOptions.All;
        }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Address")]
        [Category("Input.B")]
        public InArgument<string> Address { get; set; }

        [DisplayName("Cell")]
        [Category("Input.B")]
        public InArgument<ExcelCell> Cell { get; set; }

        [DisplayName("Clear Options")]
        [Category("Input.C")]
        [DefaultValue(ExcelClearOptions.All)]
        public ExcelClearOptions ClearOptions { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ExcelWorkbook wb = ResolveWorkbook(context);
            ExcelCell cell = ExcelActivityHelper.IsBound(Cell) ? Cell.Get(context) : null;
            string sheet = ExcelOperations.ResolveSheetName(
                wb,
                ExcelActivityHelper.GetOptionalString(SheetName, context),
                cell,
                null);
            string address = ExcelActivityHelper.GetOptionalString(Address, context);
            if (string.IsNullOrEmpty(address) && cell != null)
            {
                address = cell.Address;
                if (cell.Workbook != null)
                {
                    wb = cell.Workbook;
                }
            }

            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentException("Cell or Address is required.");
            }

            ExcelOperations.Clear(wb, sheet, address, ClearOptions, isCell: true);
            ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
        }
    }
}
