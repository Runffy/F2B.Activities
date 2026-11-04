using System.Activities;
using System.ComponentModel;

namespace F2B.Excel.CXML
{
    /// <summary>
    /// Shared Workbook InOut + FilePath resolution for standalone-executable activities.
    /// Lifetime: ① FilePath only = ephemeral; ② FilePath+Workbook = keep open; ③ Workbook only = attach.
    /// </summary>
    public abstract class ExcelWorkbookActivity : CodeActivity
    {
        [DisplayName("Workbook")]
        [Category("Input.A")]
        [Description("In-out workbook handle. Takes priority over FilePath when both are set. When bound with FilePath, the opened workbook is assigned and kept open.")]
        public InOutArgument<ExcelWorkbook> Workbook { get; set; }

        [DisplayName("File Path")]
        [Category("Input.A")]
        [Description("Optional .xlsx/.xlsm path. Alone: open → operate → close (save if write). With Workbook: open and assign handle.")]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("Password")]
        [Category("Input.A")]
        [Description("Optional. Used when opening via File Path and the file has file-level encryption.")]
        public InArgument<string> Password { get; set; }

        /// <param name="writes">True for mutating activities (ephemeral mode will Save then Close).</param>
        internal ExcelWorkbookLease LeaseWorkbook(CodeActivityContext context, bool writes)
        {
            ExcelWorkbook existing = ExcelActivityHelper.GetOptionalWorkbook(Workbook, context);
            string path = ExcelActivityHelper.GetOptionalString(FilePath, context);
            string password = ExcelActivityHelper.GetOptionalString(Password, context);
            bool bound = ExcelActivityHelper.IsBound(Workbook);
            return ExcelWorkbookLease.Acquire(
                existing,
                path,
                password,
                bound,
                wb => ExcelActivityHelper.SetWorkbook(Workbook, context, wb),
                writes);
        }

        /// <summary>
        /// Handle-producing activities must not run in ephemeral mode (filepath-only),
        /// or the returned Cell/Range would point at a disposed workbook.
        /// </summary>
        internal static void EnsureNotEphemeralForHandles(ExcelWorkbookLease lease, string activityName)
        {
            if (lease != null && lease.IsEphemeral)
            {
                throw new System.InvalidOperationException(
                    activityName
                    + " cannot use File Path alone because it returns a Cell/Range handle. "
                    + "Bind Workbook together with File Path (or pass an already-open Workbook).");
            }
        }
    }
}
