using System.Drawing;
using System.Windows.Forms;

namespace F2B.Forms.Model
{
    public enum TextHAlign
    {
        Left = 0,
        Center = 1,
        Right = 2
    }

    public enum TextVAlign
    {
        Top = 0,
        Middle = 1,
        Bottom = 2
    }

    public static class TextAlignUtil
    {
        public static TextHAlign ParseH(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TextHAlign.Left;
            }

            TextHAlign parsed;
            return System.Enum.TryParse(value.Trim(), true, out parsed) ? parsed : TextHAlign.Left;
        }

        public static TextVAlign ParseV(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TextVAlign.Middle;
            }

            // Accept "Center" as alias for Middle
            if (string.Equals(value.Trim(), "Center", System.StringComparison.OrdinalIgnoreCase))
            {
                return TextVAlign.Middle;
            }

            TextVAlign parsed;
            return System.Enum.TryParse(value.Trim(), true, out parsed) ? parsed : TextVAlign.Middle;
        }

        public static ContentAlignment ToContentAlignment(TextHAlign h, TextVAlign v)
        {
            if (v == TextVAlign.Top)
            {
                if (h == TextHAlign.Center) return ContentAlignment.TopCenter;
                if (h == TextHAlign.Right) return ContentAlignment.TopRight;
                return ContentAlignment.TopLeft;
            }

            if (v == TextVAlign.Bottom)
            {
                if (h == TextHAlign.Center) return ContentAlignment.BottomCenter;
                if (h == TextHAlign.Right) return ContentAlignment.BottomRight;
                return ContentAlignment.BottomLeft;
            }

            if (h == TextHAlign.Center) return ContentAlignment.MiddleCenter;
            if (h == TextHAlign.Right) return ContentAlignment.MiddleRight;
            return ContentAlignment.MiddleLeft;
        }

        public static HorizontalAlignment ToHorizontalAlignment(TextHAlign h)
        {
            if (h == TextHAlign.Center) return HorizontalAlignment.Center;
            if (h == TextHAlign.Right) return HorizontalAlignment.Right;
            return HorizontalAlignment.Left;
        }

        public static TextFormatFlags ToTextFormatFlags(TextHAlign h, TextVAlign v, bool wordBreak)
        {
            TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            if (wordBreak)
            {
                flags |= TextFormatFlags.WordBreak;
            }

            if (h == TextHAlign.Center) flags |= TextFormatFlags.HorizontalCenter;
            else if (h == TextHAlign.Right) flags |= TextFormatFlags.Right;
            else flags |= TextFormatFlags.Left;

            if (v == TextVAlign.Top) flags |= TextFormatFlags.Top;
            else if (v == TextVAlign.Bottom) flags |= TextFormatFlags.Bottom;
            else flags |= TextFormatFlags.VerticalCenter;

            return flags;
        }
    }
}
