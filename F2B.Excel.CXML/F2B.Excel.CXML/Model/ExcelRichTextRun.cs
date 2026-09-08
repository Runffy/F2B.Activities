using System.Drawing;

namespace F2B.Excel.CXML
{
    /// <summary>
    /// One rich-text run for Cell-SetRichText.
    /// </summary>
    public sealed class ExcelRichTextRun
    {
        public string Text { get; set; }

        public bool? Bold { get; set; }

        public bool? Italic { get; set; }

        public bool? Underline { get; set; }

        public string FontName { get; set; }

        public double? FontSize { get; set; }

        public Color? FontColor { get; set; }
    }
}
