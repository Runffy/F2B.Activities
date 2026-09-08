using System.Activities;
using System.ComponentModel;

namespace F2B.Excel.CXML
{
    [DisplayName("Workbook-Open")]
    [Description("Open an existing .xlsx/.xlsm file (shared read). Outputs Workbook handle.")]
    public sealed class WorkbookOpenActivity : CodeActivity
    {
        public WorkbookOpenActivity()
        {
            DisplayName = "Workbook-Open";
        }

        [DisplayName("File Path")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("Workbook")]
        [Category("Output")]
        public InOutArgument<ExcelWorkbook> Workbook { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            string path = ExcelActivityHelper.RequireNonEmpty(FilePath, context, nameof(FilePath));
            ExcelWorkbook wb = ExcelWorkbookSession.OpenSharedRead(path);
            ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
        }
    }

    [DisplayName("Workbook-Close")]
    [Description("Close workbook. Optionally save changes first.")]
    public sealed class WorkbookCloseActivity : CodeActivity
    {
        public WorkbookCloseActivity()
        {
            DisplayName = "Workbook-Close";
            SaveChanges = true;
        }

        [DisplayName("Workbook")]
        [Category("Input.A")]
        public InOutArgument<ExcelWorkbook> Workbook { get; set; }

        [DisplayName("File Path")]
        [Category("Input.A")]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("Save Changes")]
        [Category("Input.B")]
        [DefaultValue(true)]
        public InArgument<bool> SaveChanges { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ExcelWorkbook existing = ExcelActivityHelper.GetOptionalWorkbook(Workbook, context);
            string path = ExcelActivityHelper.GetOptionalString(FilePath, context);
            ExcelWorkbook wb = ExcelWorkbookSession.Acquire(existing, path).Workbook;
            bool save = ExcelActivityHelper.GetOrDefault(SaveChanges, context, true);
            ExcelOperations.Close(wb, save);
            ExcelActivityHelper.SetWorkbook(Workbook, context, null);
        }
    }

    [DisplayName("Workbook-Save")]
    public sealed class WorkbookSaveActivity : ExcelWorkbookActivity
    {
        public WorkbookSaveActivity()
        {
            DisplayName = "Workbook-Save";
        }

        protected override void Execute(CodeActivityContext context)
        {
            ExcelWorkbook wb = ResolveWorkbook(context);
            ExcelOperations.Save(wb);
            ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
        }
    }

    [DisplayName("Workbook-SaveAs")]
    public sealed class WorkbookSaveAsActivity : ExcelWorkbookActivity
    {
        public WorkbookSaveAsActivity()
        {
            DisplayName = "Workbook-SaveAs";
            Overwrite = true;
        }

        [DisplayName("Save As Path")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> SaveAsPath { get; set; }

        [DisplayName("Overwrite")]
        [Category("Input.B")]
        [DefaultValue(true)]
        public InArgument<bool> Overwrite { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ExcelWorkbook wb = ResolveWorkbook(context);
            string path = ExcelActivityHelper.RequireNonEmpty(SaveAsPath, context, nameof(SaveAsPath));
            bool overwrite = ExcelActivityHelper.GetOrDefault(Overwrite, context, true);
            ExcelOperations.SaveAs(wb, path, overwrite);
            ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
        }
    }

    [DisplayName("Workbook-NewSheet")]
    public sealed class WorkbookNewSheetActivity : ExcelWorkbookActivity
    {
        public WorkbookNewSheetActivity()
        {
            DisplayName = "Workbook-NewSheet";
        }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        [Description("Optional. Empty generates Sheet1, Sheet2, ...")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Created Sheet Name")]
        [Category("Output")]
        public OutArgument<string> CreatedSheetName { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ExcelWorkbook wb = ResolveWorkbook(context);
            string name = ExcelActivityHelper.GetOptionalString(SheetName, context);
            string created = ExcelOperations.NewSheet(wb, name);
            ExcelActivityHelper.SetIfBound(CreatedSheetName, context, created);
            ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
        }
    }

    [DisplayName("Workbook-DeleteSheet")]
    public sealed class WorkbookDeleteSheetActivity : ExcelWorkbookActivity
    {
        public WorkbookDeleteSheetActivity()
        {
            DisplayName = "Workbook-DeleteSheet";
        }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Sheet Index")]
        [Category("Input.B")]
        [Description("0-based. Used when Sheet Name is empty.")]
        public InArgument<int?> SheetIndex { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ExcelWorkbook wb = ResolveWorkbook(context);
            string name = ExcelActivityHelper.GetOptionalString(SheetName, context);
            int? index = ExcelActivityHelper.GetOrDefault(SheetIndex, context, null);
            ExcelOperations.DeleteSheet(wb, name, index);
            ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
        }
    }

    [DisplayName("Workbook-GetAllSheets")]
    public sealed class WorkbookGetAllSheetsActivity : ExcelWorkbookActivity
    {
        public WorkbookGetAllSheetsActivity()
        {
            DisplayName = "Workbook-GetAllSheets";
        }

        [DisplayName("Sheet Names")]
        [Category("Output")]
        public OutArgument<string[]> SheetNames { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ExcelWorkbook wb = ResolveWorkbook(context);
            ExcelActivityHelper.SetIfBound(SheetNames, context, ExcelOperations.GetAllSheetNames(wb));
            ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
        }
    }

    [DisplayName("Workbook-ActivateSheet")]
    public sealed class WorkbookActivateSheetActivity : ExcelWorkbookActivity
    {
        public WorkbookActivateSheetActivity()
        {
            DisplayName = "Workbook-ActivateSheet";
        }

        [DisplayName("Sheet Name")]
        [Category("Input.B")]
        [Description("Takes priority when non-empty.")]
        public InArgument<string> SheetName { get; set; }

        [DisplayName("Sheet Index")]
        [Category("Input.B")]
        [Description("0-based. Used when Sheet Name is empty.")]
        public InArgument<int?> SheetIndex { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ExcelWorkbook wb = ResolveWorkbook(context);
            string name = ExcelActivityHelper.GetOptionalString(SheetName, context);
            int? index = ExcelActivityHelper.GetOrDefault(SheetIndex, context, null);
            ExcelOperations.ActivateSheet(wb, name, index);
            ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
        }
    }

    [DisplayName("Workbook-GetActiveSheet")]
    public sealed class WorkbookGetActiveSheetActivity : ExcelWorkbookActivity
    {
        public WorkbookGetActiveSheetActivity()
        {
            DisplayName = "Workbook-GetActiveSheet";
        }

        [DisplayName("Sheet Name")]
        [Category("Output")]
        public OutArgument<string> SheetName { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ExcelWorkbook wb = ResolveWorkbook(context);
            ExcelActivityHelper.SetIfBound(SheetName, context, ExcelOperations.GetActiveSheetName(wb));
            ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
        }
    }

    [DisplayName("Workbook-CopySheet")]
    public sealed class WorkbookCopySheetActivity : ExcelWorkbookActivity
    {
        public WorkbookCopySheetActivity()
        {
            DisplayName = "Workbook-CopySheet";
        }

        [DisplayName("Source Sheet Name")]
        [Category("Input.B")]
        [Description("Empty = active sheet.")]
        public InArgument<string> SourceSheetName { get; set; }

        [DisplayName("New Sheet Name")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> NewSheetName { get; set; }

        [DisplayName("Created Sheet Name")]
        [Category("Output")]
        public OutArgument<string> CreatedSheetName { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ExcelWorkbook wb = ResolveWorkbook(context);
            string source = ExcelActivityHelper.GetOptionalString(SourceSheetName, context);
            string newName = ExcelActivityHelper.RequireNonEmpty(NewSheetName, context, nameof(NewSheetName));
            string created = ExcelOperations.CopySheet(wb, source, newName);
            ExcelActivityHelper.SetIfBound(CreatedSheetName, context, created);
            ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
        }
    }
}
