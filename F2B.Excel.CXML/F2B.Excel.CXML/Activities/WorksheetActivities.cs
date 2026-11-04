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
            using (ExcelWorkbookLease lease = LeaseWorkbook(context, writes: false))
            {
                string sheet = ExcelActivityHelper.GetOptionalString(SheetName, context);
                string address = ExcelActivityHelper.GetOptionalString(Address, context);
                bool headers = ExcelActivityHelper.GetOrDefault(HasHeaders, context, true);
                DataTable table = ExcelOperations.ReadRangeToDataTable(lease.Workbook, sheet, address, headers);
                ExcelActivityHelper.SetIfBound(DataTable, context, table);
            }
        }
    }

    [DisplayName("Worksheet-WriteRange")]
    [Description("Write DataTable. Sheet empty=Active; Address empty=from A1; full range clears target first. FilePath alone saves and closes.")]
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
            using (ExcelWorkbookLease lease = LeaseWorkbook(context, writes: true))
            {
                string sheet = ExcelActivityHelper.GetOptionalString(SheetName, context);
                string address = ExcelActivityHelper.GetOptionalString(Address, context);
                DataTable table = DataTable.Get(context);
                bool headers = ExcelActivityHelper.GetOrDefault(HasHeaders, context, true);
                ExcelOperations.WriteRangeFromDataTable(lease.Workbook, sheet, address, table, headers);
            }
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

        [DisplayName("Password")]
        [Category("Input.A")]
        public InArgument<string> Password { get; set; }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Address")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> Address { get; set; }

        protected override void Execute(NativeActivityContext context)
        {
            ExcelWorkbook existing = ExcelActivityHelper.GetOptionalWorkbook(Workbook, context);
            string path = ExcelActivityHelper.GetOptionalString(FilePath, context);
            string password = ExcelActivityHelper.GetOptionalString(Password, context);
            bool bound = ExcelActivityHelper.IsBound(Workbook);

            using (ExcelWorkbookLease lease = ExcelWorkbookLease.Acquire(
                existing,
                path,
                password,
                bound,
                wb => ExcelActivityHelper.SetWorkbook(Workbook, context, wb),
                writes: false))
            {
                string sheet = ExcelActivityHelper.GetOptionalString(SheetName, context);
                string address = Address.Get(context);
                object raw = ExcelOperations.ReadCellValue(lease.Workbook, sheet, address);
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

        [DisplayName("Password")]
        [Category("Input.A")]
        public InArgument<string> Password { get; set; }

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
            ExcelWorkbook existing = ExcelActivityHelper.GetOptionalWorkbook(Workbook, context);
            string path = ExcelActivityHelper.GetOptionalString(FilePath, context);
            string password = ExcelActivityHelper.GetOptionalString(Password, context);
            bool bound = ExcelActivityHelper.IsBound(Workbook);

            using (ExcelWorkbookLease lease = ExcelWorkbookLease.Acquire(
                existing,
                path,
                password,
                bound,
                wb => ExcelActivityHelper.SetWorkbook(Workbook, context, wb),
                writes: true))
            {
                string sheet = ExcelActivityHelper.GetOptionalString(SheetName, context);
                string address = Address.Get(context);
                TValue value = Value != null && Value.Expression != null ? Value.Get(context) : default(TValue);
                ExcelOperations.WriteCellValue(lease.Workbook, sheet, address, value);
            }
        }
    }

    [DisplayName("Worksheet-GetCellByAddress")]
    [Description("Returns a Cell handle. Requires Workbook binding (not FilePath alone).")]
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
            using (ExcelWorkbookLease lease = LeaseWorkbook(context, writes: false))
            {
                EnsureNotEphemeralForHandles(lease, DisplayName);
                string sheet = ExcelActivityHelper.GetOptionalString(SheetName, context);
                string address = ExcelActivityHelper.RequireNonEmpty(Address, context, nameof(Address));
                ExcelCell cell = ExcelOperations.CreateCellHandle(lease.Workbook, sheet, address);
                ExcelActivityHelper.SetIfBound(Cell, context, cell);
            }
        }
    }

    [DisplayName("Worksheet-GetRangeByAddress")]
    [Description("Returns a Range handle. Requires Workbook binding (not FilePath alone).")]
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
            using (ExcelWorkbookLease lease = LeaseWorkbook(context, writes: false))
            {
                EnsureNotEphemeralForHandles(lease, DisplayName);
                string sheet = ExcelActivityHelper.GetOptionalString(SheetName, context);
                string address = ExcelActivityHelper.RequireNonEmpty(Address, context, nameof(Address));
                ExcelRange range = ExcelOperations.CreateRangeHandle(lease.Workbook, sheet, address);
                ExcelActivityHelper.SetIfBound(Range, context, range);
            }
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
            using (ExcelWorkbookLease lease = LeaseWorkbook(context, writes: true))
            {
                ExcelWorkbook wb = lease.Workbook;
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
            }
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
            using (ExcelWorkbookLease lease = LeaseWorkbook(context, writes: true))
            {
                ExcelWorkbook wb = lease.Workbook;
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
            }
        }
    }
}
