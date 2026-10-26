using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Excel.CXML
{
    /// <summary>
    /// Shared Workbook InOut + FilePath resolution for standalone-executable activities.
    /// </summary>
    public abstract class ExcelWorkbookActivity : CodeActivity
    {
        [DisplayName("Workbook")]
        [Category("Input.A")]
        [Description("In-out workbook handle. Takes priority over FilePath when both are set.")]
        public InOutArgument<ExcelWorkbook> Workbook { get; set; }

        [DisplayName("File Path")]
        [Category("Input.A")]
        [Description("Optional .xlsx/.xlsm path used when Workbook is not provided.")]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("Password")]
        [Category("Input.A")]
        [Description("Optional. Used only when opening via File Path and the file has file-level encryption. Prefer Workbook-Open + Workbook handle.")]
        public InArgument<string> Password { get; set; }

        protected ExcelWorkbook ResolveWorkbook(CodeActivityContext context)
        {
            ExcelWorkbook existing = ExcelActivityHelper.GetOptionalWorkbook(Workbook, context);
            string path = ExcelActivityHelper.GetOptionalString(FilePath, context);
            string password = ExcelActivityHelper.GetOptionalString(Password, context);
            using (ExcelWorkbookSession session = ExcelWorkbookSession.Acquire(existing, path, password))
            {
                ExcelWorkbook wb = session.Workbook;
                ExcelActivityHelper.SetWorkbook(Workbook, context, wb);
                return wb;
            }
        }
    }
}
