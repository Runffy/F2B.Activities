using System;
using System.Drawing;
using System.Windows.Forms;
using F2B.Forms.Model;

namespace F2B.Forms.Engine
{
    /// <summary>
    /// Applies Font / ForeColor / BackColor to MixTable via DataGridViewCellStyle
    /// (Default, Column, Row, or Cell). Empty style fields mean "keep current".
    /// </summary>
    internal static class MixTableStyle
    {
        public static void ApplyControlChromeToCellStyles(DataGridView grid)
        {
            if (grid == null)
            {
                return;
            }

            Font font = grid.Font ?? SystemFonts.DefaultFont;
            Color fore = grid.ForeColor.IsEmpty ? SystemColors.ControlText : grid.ForeColor;
            Color back = grid.BackColor.IsEmpty ? SystemColors.Window : grid.BackColor;

            grid.DefaultCellStyle.Font = font;
            grid.DefaultCellStyle.ForeColor = fore;
            grid.DefaultCellStyle.BackColor = back;

            grid.ColumnHeadersDefaultCellStyle.Font = font;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = fore;
            // Keep header background as control chrome unless explicitly styled later.
            if (grid.ColumnHeadersDefaultCellStyle.BackColor.IsEmpty)
            {
                grid.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
            }

            grid.RowsDefaultCellStyle.Font = font;
            grid.RowsDefaultCellStyle.ForeColor = fore;
            grid.RowsDefaultCellStyle.BackColor = back;
        }

        public static void ApplyColumnDefinitionStyle(DataGridViewColumn column, MixTableColumnDefinition def)
        {
            if (column == null || def == null)
            {
                return;
            }

            bool hasFont = !string.IsNullOrWhiteSpace(def.FontFamily)
                || def.FontSize.HasValue
                || def.FontBold == true
                || def.FontItalic == true
                || def.FontUnderline == true;
            if (hasFont)
            {
                Font current = column.DefaultCellStyle.Font ?? SystemFonts.DefaultFont;
                string family = string.IsNullOrWhiteSpace(def.FontFamily)
                    ? current.FontFamily.Name
                    : def.FontFamily.Trim();
                float size = def.FontSize.HasValue && def.FontSize.Value > 0
                    ? def.FontSize.Value
                    : (current.Size > 0 ? current.Size : FontStyleUtil.DefaultSize);
                column.DefaultCellStyle.Font = FontStyleUtil.CreateFont(
                    family,
                    size,
                    def.FontBold == true,
                    def.FontItalic == true,
                    def.FontUnderline == true);
            }

            Color? fore = FontStyleUtil.ParseColor(def.ForeColor);
            if (fore.HasValue)
            {
                column.DefaultCellStyle.ForeColor = fore.Value;
            }

            Color? back = FontStyleUtil.ParseColor(def.BackColor);
            if (back.HasValue)
            {
                column.DefaultCellStyle.BackColor = back.Value;
            }
        }

        public static void ApplyPartial(
            DataGridViewCellStyle style,
            Font baseFont,
            string fontFamily,
            float? fontSize,
            bool? bold,
            bool? italic,
            bool? underline,
            string foreColor,
            string backColor)
        {
            if (style == null)
            {
                throw new ArgumentNullException(nameof(style));
            }

            Font current = style.Font ?? baseFont ?? SystemFonts.DefaultFont;
            bool familyChanged = !string.IsNullOrWhiteSpace(fontFamily);
            bool sizeChanged = fontSize.HasValue && fontSize.Value > 0;
            bool styleChanged = bold.HasValue || italic.HasValue || underline.HasValue;

            if (familyChanged || sizeChanged || styleChanged)
            {
                string family = familyChanged ? fontFamily.Trim() : current.FontFamily.Name;
                float size = sizeChanged
                    ? fontSize.Value
                    : (current.Size > 0 ? current.Size : FontStyleUtil.DefaultSize);
                bool useBold = bold ?? current.Bold;
                bool useItalic = italic ?? current.Italic;
                bool useUnderline = underline ?? current.Underline;
                style.Font = FontStyleUtil.CreateFont(family, size, useBold, useItalic, useUnderline);
            }

            Color? fore = FontStyleUtil.ParseColor(foreColor);
            if (fore.HasValue)
            {
                style.ForeColor = fore.Value;
            }

            Color? back = FontStyleUtil.ParseColor(backColor);
            if (back.HasValue)
            {
                style.BackColor = back.Value;
            }
        }

        public static string NormalizeScope(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                return "Default";
            }

            string s = scope.Trim();
            if (string.Equals(s, "Default", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Table", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "All", StringComparison.OrdinalIgnoreCase))
            {
                return "Default";
            }

            if (string.Equals(s, "Column", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Col", StringComparison.OrdinalIgnoreCase))
            {
                return "Column";
            }

            if (string.Equals(s, "Row", StringComparison.OrdinalIgnoreCase))
            {
                return "Row";
            }

            if (string.Equals(s, "Cell", StringComparison.OrdinalIgnoreCase))
            {
                return "Cell";
            }

            throw new ArgumentException(
                "MixTable style scope must be Default | Column | Row | Cell. Value: '" + scope + "'.",
                nameof(scope));
        }
    }
}
