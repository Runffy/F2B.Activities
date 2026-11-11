using System;

namespace F2B.Forms.Engine
{
    /// <summary>
    /// Virtual control ids for MixTable cells, usable with Get/Set Control Value/Text.
    /// Format: mixcell|{tableId}|{rowIndex}|{columnName}
    /// </summary>
    public static class MixTableCellId
    {
        public const string Prefix = "mixcell|";

        public static string Create(string tableId, int rowIndex, string columnName)
        {
            if (string.IsNullOrWhiteSpace(tableId))
            {
                throw new ArgumentException("Table id is required.", nameof(tableId));
            }

            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException("Column name is required.", nameof(columnName));
            }

            if (rowIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rowIndex));
            }

            return Prefix + tableId.Trim() + "|" + rowIndex + "|" + columnName.Trim();
        }

        public static bool IsMixTableCellId(string controlId)
        {
            return !string.IsNullOrWhiteSpace(controlId)
                && controlId.TrimStart().StartsWith(Prefix, StringComparison.Ordinal);
        }

        public static bool TryParse(
            string controlId,
            out string tableId,
            out int rowIndex,
            out string columnName)
        {
            tableId = null;
            rowIndex = -1;
            columnName = null;

            if (!IsMixTableCellId(controlId))
            {
                return false;
            }

            string text = controlId.Trim();
            // mixcell|{table}|{row}|{column} — column may contain '|' rarely; split max 4 parts.
            string[] parts = text.Split(new[] { '|' }, 4);
            if (parts.Length != 4)
            {
                return false;
            }

            if (!string.Equals(parts[0], "mixcell", StringComparison.Ordinal))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[3]))
            {
                return false;
            }

            if (!int.TryParse(parts[2], out rowIndex) || rowIndex < 0)
            {
                return false;
            }

            tableId = parts[1];
            columnName = parts[3];
            return true;
        }
    }
}
