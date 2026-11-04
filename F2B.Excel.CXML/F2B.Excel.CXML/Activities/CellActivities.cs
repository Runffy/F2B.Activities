using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace F2B.Excel.CXML
{
    internal static class ExcelCellTarget
    {
        internal static ExcelWorkbookLease Resolve(
            CodeActivityContext context,
            InOutArgument<ExcelWorkbook> workbookArg,
            InArgument<string> filePathArg,
            InArgument<string> passwordArg,
            InArgument<ExcelCell> cellArg,
            InArgument<string> sheetArg,
            InArgument<string> addressArg,
            bool writes,
            out string sheet,
            out string address)
        {
            ExcelCell cell = ExcelActivityHelper.IsBound(cellArg) ? cellArg.Get(context) : null;
            ExcelWorkbook existing = ExcelActivityHelper.GetOptionalWorkbook(workbookArg, context);
            if (existing == null && cell != null)
            {
                existing = cell.Workbook;
            }

            string path = ExcelActivityHelper.GetOptionalString(filePathArg, context);
            string password = ExcelActivityHelper.GetOptionalString(passwordArg, context);
            bool bound = ExcelActivityHelper.IsBound(workbookArg);

            ExcelWorkbookLease lease = ExcelWorkbookLease.Acquire(
                existing,
                path,
                password,
                bound,
                wb => ExcelActivityHelper.SetWorkbook(workbookArg, context, wb),
                writes);

            sheet = ExcelOperations.ResolveSheetName(
                lease.Workbook,
                ExcelActivityHelper.GetOptionalString(sheetArg, context),
                cell,
                null);
            address = ExcelActivityHelper.GetOptionalString(addressArg, context);
            if (string.IsNullOrEmpty(address) && cell != null)
            {
                address = cell.Address;
            }

            if (string.IsNullOrEmpty(address))
            {
                lease.Dispose();
                throw new ArgumentException("Cell or Address is required.");
            }

            return lease;
        }
    }

    [DisplayName("Cell-SetFormula")]
    public sealed class CellSetFormulaActivity : ExcelWorkbookActivity
    {
        public CellSetFormulaActivity()
        {
            DisplayName = "Cell-SetFormula";
        }

        [DisplayName("Cell")]
        [Category("Input.B")]
        public InArgument<ExcelCell> Cell { get; set; }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Address")]
        [Category("Input.B")]
        public InArgument<string> Address { get; set; }

        [DisplayName("Formula")]
        [RequiredArgument]
        [Category("Input.C")]
        [Description("Leading '=' is optional. Formula is written only; not calculated.")]
        public InArgument<string> Formula { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            using (ExcelWorkbookLease lease = ExcelCellTarget.Resolve(
                context, Workbook, FilePath, Password, Cell, SheetName, Address, writes: true,
                out string sheet, out string address))
            {
                string formula = ExcelActivityHelper.RequireNonEmpty(Formula, context, nameof(Formula));
                ExcelOperations.SetFormula(lease.Workbook, sheet, address, formula);
            }
        }
    }

    [DisplayName("Cell-SetBackgroundColor")]
    public sealed class CellSetBackgroundColorActivity : ExcelWorkbookActivity
    {
        public CellSetBackgroundColorActivity()
        {
            DisplayName = "Cell-SetBackgroundColor";
        }

        [DisplayName("Cell")]
        [Category("Input.B")]
        public InArgument<ExcelCell> Cell { get; set; }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Address")]
        [Category("Input.B")]
        public InArgument<string> Address { get; set; }

        [DisplayName("Color")]
        [RequiredArgument]
        [Category("Input.C")]
        public InArgument<Color> Color { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            using (ExcelWorkbookLease lease = ExcelCellTarget.Resolve(
                context, Workbook, FilePath, Password, Cell, SheetName, Address, writes: true,
                out string sheet, out string address))
            {
                Color color = Color.Get(context);
                ExcelOperations.SetBackgroundColor(lease.Workbook, sheet, address, color);
            }
        }
    }

    [DisplayName("Cell-SetForegroundColor")]
    public sealed class CellSetForegroundColorActivity : ExcelWorkbookActivity
    {
        public CellSetForegroundColorActivity()
        {
            DisplayName = "Cell-SetForegroundColor";
        }

        [DisplayName("Cell")]
        [Category("Input.B")]
        public InArgument<ExcelCell> Cell { get; set; }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Address")]
        [Category("Input.B")]
        public InArgument<string> Address { get; set; }

        [DisplayName("Color")]
        [RequiredArgument]
        [Category("Input.C")]
        public InArgument<Color> Color { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            using (ExcelWorkbookLease lease = ExcelCellTarget.Resolve(
                context, Workbook, FilePath, Password, Cell, SheetName, Address, writes: true,
                out string sheet, out string address))
            {
                Color color = Color.Get(context);
                ExcelOperations.SetFontColor(lease.Workbook, sheet, address, color);
            }
        }
    }

    [DisplayName("Cell-SetFontFormat")]
    public sealed class CellSetFontFormatActivity : ExcelWorkbookActivity
    {
        public CellSetFontFormatActivity()
        {
            DisplayName = "Cell-SetFontFormat";
        }

        [DisplayName("Cell")]
        [Category("Input.B")]
        public InArgument<ExcelCell> Cell { get; set; }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Address")]
        [Category("Input.B")]
        public InArgument<string> Address { get; set; }

        [DisplayName("Font Name")]
        [Category("Input.C")]
        public InArgument<string> FontName { get; set; }

        [DisplayName("Font Size")]
        [Category("Input.C")]
        public InArgument<double?> FontSize { get; set; }

        [DisplayName("Bold")]
        [Category("Input.C")]
        public InArgument<bool?> Bold { get; set; }

        [DisplayName("Italic")]
        [Category("Input.C")]
        public InArgument<bool?> Italic { get; set; }

        [DisplayName("Underline")]
        [Category("Input.C")]
        public InArgument<bool?> Underline { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            using (ExcelWorkbookLease lease = ExcelCellTarget.Resolve(
                context, Workbook, FilePath, Password, Cell, SheetName, Address, writes: true,
                out string sheet, out string address))
            {
                ExcelOperations.SetFontFormat(
                    lease.Workbook,
                    sheet,
                    address,
                    ExcelActivityHelper.GetOptionalString(FontName, context),
                    ExcelActivityHelper.GetOrDefault(FontSize, context, null),
                    ExcelActivityHelper.GetOrDefault(Bold, context, null),
                    ExcelActivityHelper.GetOrDefault(Italic, context, null),
                    ExcelActivityHelper.GetOrDefault(Underline, context, null));
            }
        }
    }

    [DisplayName("Cell-SetRichText")]
    public sealed class CellSetRichTextActivity : ExcelWorkbookActivity
    {
        public CellSetRichTextActivity()
        {
            DisplayName = "Cell-SetRichText";
            Replace = true;
        }

        [DisplayName("Cell")]
        [Category("Input.B")]
        public InArgument<ExcelCell> Cell { get; set; }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Address")]
        [Category("Input.B")]
        public InArgument<string> Address { get; set; }

        [DisplayName("Runs")]
        [RequiredArgument]
        [Category("Input.C")]
        public InArgument<List<ExcelRichTextRun>> Runs { get; set; }

        [DisplayName("Replace")]
        [Category("Input.C")]
        [DefaultValue(true)]
        public InArgument<bool> Replace { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            using (ExcelWorkbookLease lease = ExcelCellTarget.Resolve(
                context, Workbook, FilePath, Password, Cell, SheetName, Address, writes: true,
                out string sheet, out string address))
            {
                List<ExcelRichTextRun> runs = Runs.Get(context);
                bool replace = ExcelActivityHelper.GetOrDefault(Replace, context, true);
                ExcelOperations.SetRichText(lease.Workbook, sheet, address, runs, replace);
            }
        }
    }

    [DisplayName("Cell-GetAddress")]
    [Description("Builds address/Cell handle. Requires Workbook binding (not FilePath alone).")]
    public sealed class CellGetAddressActivity : ExcelWorkbookActivity
    {
        public CellGetAddressActivity()
        {
            DisplayName = "Cell-GetAddress";
        }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Row Index")]
        [RequiredArgument]
        [Category("Input.B")]
        [Description("1-based")]
        public InArgument<int> RowIndex { get; set; }

        [DisplayName("Column Index")]
        [RequiredArgument]
        [Category("Input.B")]
        [Description("1-based")]
        public InArgument<int> ColumnIndex { get; set; }

        [DisplayName("Address")]
        [Category("Output")]
        public OutArgument<string> Address { get; set; }

        [DisplayName("Cell")]
        [Category("Output")]
        public OutArgument<ExcelCell> Cell { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            using (ExcelWorkbookLease lease = LeaseWorkbook(context, writes: false))
            {
                EnsureNotEphemeralForHandles(lease, DisplayName);
                string sheet = ExcelOperations.ResolveSheetName(
                    lease.Workbook,
                    ExcelActivityHelper.GetOptionalString(SheetName, context),
                    null,
                    null);
                ExcelOperations.GetWorksheet(lease.Workbook, sheet);
                int row = RowIndex.Get(context);
                int col = ColumnIndex.Get(context);
                ExcelCell cell = ExcelAddressUtil.CreateCell(lease.Workbook, sheet, row, col);
                ExcelActivityHelper.SetIfBound(Address, context, cell.Address);
                ExcelActivityHelper.SetIfBound(Cell, context, cell);
            }
        }
    }

    [DisplayName("Range-GetAddress")]
    [Description("Builds address/Range handle. Requires Workbook binding (not FilePath alone).")]
    public sealed class RangeGetAddressActivity : ExcelWorkbookActivity
    {
        public RangeGetAddressActivity()
        {
            DisplayName = "Range-GetAddress";
        }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Start Row Index")]
        [RequiredArgument]
        [Category("Input.B")]
        [Description("1-based")]
        public InArgument<int> StartRowIndex { get; set; }

        [DisplayName("Start Column Index")]
        [RequiredArgument]
        [Category("Input.B")]
        [Description("1-based")]
        public InArgument<int> StartColumnIndex { get; set; }

        [DisplayName("End Row Index")]
        [RequiredArgument]
        [Category("Input.B")]
        [Description("1-based")]
        public InArgument<int> EndRowIndex { get; set; }

        [DisplayName("End Column Index")]
        [RequiredArgument]
        [Category("Input.B")]
        [Description("1-based")]
        public InArgument<int> EndColumnIndex { get; set; }

        [DisplayName("Address")]
        [Category("Output")]
        public OutArgument<string> Address { get; set; }

        [DisplayName("Range")]
        [Category("Output")]
        public OutArgument<ExcelRange> Range { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            using (ExcelWorkbookLease lease = LeaseWorkbook(context, writes: false))
            {
                EnsureNotEphemeralForHandles(lease, DisplayName);
                string sheet = ExcelOperations.ResolveSheetName(
                    lease.Workbook,
                    ExcelActivityHelper.GetOptionalString(SheetName, context),
                    null,
                    null);
                ExcelOperations.GetWorksheet(lease.Workbook, sheet);
                ExcelRange range = ExcelAddressUtil.CreateRange(
                    lease.Workbook,
                    sheet,
                    StartRowIndex.Get(context),
                    StartColumnIndex.Get(context),
                    EndRowIndex.Get(context),
                    EndColumnIndex.Get(context));
                ExcelActivityHelper.SetIfBound(Address, context, range.Address);
                ExcelActivityHelper.SetIfBound(Range, context, range);
            }
        }
    }
}
