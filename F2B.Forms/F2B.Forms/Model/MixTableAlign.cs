using System;
using System.Windows.Forms;

namespace F2B.Forms.Model
{
    /// <summary>
    /// Parses MixTable header/cell align tokens such as left-top, middle-center, right-bottom.
    /// </summary>
    public static class MixTableAlign
    {
        public static DataGridViewContentAlignment Parse(
            string value,
            DataGridViewContentAlignment fallback = DataGridViewContentAlignment.MiddleCenter)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string token = value.Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');

            // Allow WinForms enum names too (case-insensitive).
            DataGridViewContentAlignment named;
            if (Enum.TryParse(value.Trim(), true, out named)
                && named != DataGridViewContentAlignment.NotSet)
            {
                return named;
            }

            switch (token)
            {
                case "left-top":
                case "top-left":
                    return DataGridViewContentAlignment.TopLeft;
                case "middle-top":
                case "center-top":
                case "top-middle":
                case "top-center":
                    return DataGridViewContentAlignment.TopCenter;
                case "right-top":
                case "top-right":
                    return DataGridViewContentAlignment.TopRight;
                case "left-center":
                case "left-middle":
                case "middle-left":
                case "center-left":
                    return DataGridViewContentAlignment.MiddleLeft;
                case "middle-center":
                case "center-middle":
                case "center":
                case "middle":
                    return DataGridViewContentAlignment.MiddleCenter;
                case "right-center":
                case "right-middle":
                case "middle-right":
                case "center-right":
                    return DataGridViewContentAlignment.MiddleRight;
                case "left-bottom":
                case "bottom-left":
                    return DataGridViewContentAlignment.BottomLeft;
                case "middle-bottom":
                case "center-bottom":
                case "bottom-middle":
                case "bottom-center":
                    return DataGridViewContentAlignment.BottomCenter;
                case "right-bottom":
                case "bottom-right":
                    return DataGridViewContentAlignment.BottomRight;
                case "left":
                    return DataGridViewContentAlignment.MiddleLeft;
                case "right":
                    return DataGridViewContentAlignment.MiddleRight;
                case "top":
                    return DataGridViewContentAlignment.TopCenter;
                case "bottom":
                    return DataGridViewContentAlignment.BottomCenter;
                default:
                    return fallback;
            }
        }

        public static System.Drawing.ContentAlignment ToDrawingAlignment(DataGridViewContentAlignment align)
        {
            switch (align)
            {
                case DataGridViewContentAlignment.TopLeft:
                    return System.Drawing.ContentAlignment.TopLeft;
                case DataGridViewContentAlignment.TopCenter:
                    return System.Drawing.ContentAlignment.TopCenter;
                case DataGridViewContentAlignment.TopRight:
                    return System.Drawing.ContentAlignment.TopRight;
                case DataGridViewContentAlignment.MiddleLeft:
                    return System.Drawing.ContentAlignment.MiddleLeft;
                case DataGridViewContentAlignment.MiddleCenter:
                    return System.Drawing.ContentAlignment.MiddleCenter;
                case DataGridViewContentAlignment.MiddleRight:
                    return System.Drawing.ContentAlignment.MiddleRight;
                case DataGridViewContentAlignment.BottomLeft:
                    return System.Drawing.ContentAlignment.BottomLeft;
                case DataGridViewContentAlignment.BottomCenter:
                    return System.Drawing.ContentAlignment.BottomCenter;
                case DataGridViewContentAlignment.BottomRight:
                    return System.Drawing.ContentAlignment.BottomRight;
                default:
                    return System.Drawing.ContentAlignment.MiddleLeft;
            }
        }

        public static System.Windows.Forms.TextFormatFlags ToTextFormatFlags(DataGridViewContentAlignment align)
        {
            TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            switch (align)
            {
                case DataGridViewContentAlignment.TopLeft:
                    return flags | TextFormatFlags.Left | TextFormatFlags.Top;
                case DataGridViewContentAlignment.TopCenter:
                    return flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.Top;
                case DataGridViewContentAlignment.TopRight:
                    return flags | TextFormatFlags.Right | TextFormatFlags.Top;
                case DataGridViewContentAlignment.MiddleLeft:
                    return flags | TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
                case DataGridViewContentAlignment.MiddleCenter:
                    return flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
                case DataGridViewContentAlignment.MiddleRight:
                    return flags | TextFormatFlags.Right | TextFormatFlags.VerticalCenter;
                case DataGridViewContentAlignment.BottomLeft:
                    return flags | TextFormatFlags.Left | TextFormatFlags.Bottom;
                case DataGridViewContentAlignment.BottomCenter:
                    return flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom;
                case DataGridViewContentAlignment.BottomRight:
                    return flags | TextFormatFlags.Right | TextFormatFlags.Bottom;
                default:
                    return flags | TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
            }
        }
    }
}
