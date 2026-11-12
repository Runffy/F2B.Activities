using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using F2B.Forms.Model;

namespace F2B.Forms.Designer
{
    /// <summary>
    /// PropertyGrid modal editor for <see cref="DesignItem.InitialRowsJson"/>:
    /// click … to open <see cref="MixTableInitialRowsGeneratorForm"/>.
    /// Requires Columns Json with at least one valid column.
    /// </summary>
    public sealed class MixTableInitialRowsGeneratorEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(
            ITypeDescriptorContext context,
            IServiceProvider provider,
            object value)
        {
            if (context == null || provider == null)
            {
                return value;
            }

            DesignItem item = context.Instance as DesignItem;
            if (item != null && item.ViewOnlyProperties)
            {
                MessageBox.Show(
                    "Initial Rows Generator is only available in Designer mode.",
                    "MixTable Initial Rows Generator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return value;
            }

            string columnsJson = item != null ? item.ColumnsJson : null;
            List<MixTableColumnDefinition> columns;
            string columnsError;
            if (!MixTableInitialRowsGeneratorForm.TryGetRequiredColumns(
                columnsJson,
                out columns,
                out columnsError))
            {
                MessageBox.Show(
                    columnsError,
                    "MixTable Initial Rows Generator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return value;
            }

            string currentJson = value as string;
            if (item != null && FormControlType.IsMixTable(item.Type))
            {
                currentJson = item.InitialRowsJson;
            }

            string rowsError;
            if (!MixTableInitialRowsGeneratorForm.TryValidateInitialRowsJson(currentJson, out rowsError))
            {
                MessageBox.Show(
                    "Initial Rows Json is invalid.\r\n\r\n" + rowsError
                    + "\r\n\r\nFix the JSON syntax (or clear Initial Rows Json), then open the Generator again.",
                    "MixTable Initial Rows Generator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return value;
            }

            var editorService = provider.GetService(typeof(IWindowsFormsEditorService))
                as IWindowsFormsEditorService;
            if (editorService == null)
            {
                return value;
            }

            string titleSuffix = item != null && !string.IsNullOrWhiteSpace(item.Id)
                ? item.Id
                : "MixTable";

            using (var dialog = new MixTableInitialRowsGeneratorForm(columns, currentJson))
            {
                dialog.Text = "MixTable Initial Rows Json Generator — " + titleSuffix;
                if (editorService.ShowDialog(dialog) != DialogResult.OK)
                {
                    return value;
                }

                return dialog.GeneratedJson ?? "[]";
            }
        }
    }
}
