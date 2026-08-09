using System;
using System.Drawing;

namespace F2B.Forms.Model
{
    public static class FontStyleUtil
    {
        public const string DefaultFamily = "Microsoft YaHei UI";
        public const float DefaultSize = 9f;

        public static Font CreateFont(string family, float size, bool bold, bool italic, bool underline = false)
        {
            float emSize = size > 0 ? size : DefaultSize;
            FontStyle style = FontStyle.Regular;
            if (bold)
            {
                style |= FontStyle.Bold;
            }

            if (italic)
            {
                style |= FontStyle.Italic;
            }

            if (underline)
            {
                style |= FontStyle.Underline;
            }

            string name = string.IsNullOrWhiteSpace(family) ? DefaultFamily : family.Trim();
            try
            {
                return new Font(name, emSize, style, GraphicsUnit.Point);
            }
            catch
            {
                try
                {
                    return new Font(SystemFonts.DefaultFont.FontFamily, emSize, style, GraphicsUnit.Point);
                }
                catch
                {
                    return (Font)SystemFonts.DefaultFont.Clone();
                }
            }
        }

        public static Color? ParseColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                return ColorTranslator.FromHtml(value.Trim());
            }
            catch
            {
                try
                {
                    return Color.FromName(value.Trim());
                }
                catch
                {
                    return null;
                }
            }
        }

        public static string ToHtmlColor(Color color)
        {
            if (color.IsEmpty)
            {
                return null;
            }

            if (color.A == 255)
            {
                return ColorTranslator.ToHtml(color);
            }

            return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", color.A, color.R, color.G, color.B);
        }
    }
}
