using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using F2B.Forms.Model;

namespace F2B.Forms.Designer
{
    /// <summary>
    /// PropertyGrid modal editor for <see cref="DesignItem.ColumnsJson"/>:
    /// click … to open <see cref="MixTableColumnsGeneratorForm"/>.
    /// </summary>
    public sealed class MixTableColumnsGeneratorEditor : UITypeEditor
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
                    "Columns Generator is only available in Designer mode.",
                    "MixTable Columns Generator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return value;
            }

            string currentJson = value as string;
            if (item != null && FormControlType.IsMixTable(item.Type))
            {
                currentJson = item.ColumnsJson;
            }

            string error;
            if (!MixTableColumnsGeneratorForm.TryValidateColumnsJson(currentJson, out error))
            {
                MessageBox.Show(
                    "Columns Json is invalid.\r\n\r\n" + error
                    + "\r\n\r\nFix the JSON syntax (or clear Columns Json), then open the Generator again.",
                    "MixTable Columns Generator",
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

            using (var dialog = new MixTableColumnsGeneratorForm(currentJson))
            {
                dialog.Text = "MixTable Columns Json Generator — " + titleSuffix;
                if (editorService.ShowDialog(dialog) != DialogResult.OK)
                {
                    return value;
                }

                return dialog.GeneratedJson ?? "[]";
            }
        }
    }
}
