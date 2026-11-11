using System;

namespace F2B.Forms.Model
{
    /// <summary>
    /// Per-cell display mode for MixTable. Column type stays the same; this cell may hide the editor.
    /// </summary>
    public static class MixTableCellDisplayMode
    {
        /// <summary>Show the column's normal control (ComboBox / CheckBox / Button / ...).</summary>
        public const string Control = "Control";

        /// <summary>Show read-only plain text instead of the editor control.</summary>
        public const string Text = "Text";

        /// <summary>Do not show the control (blank cell); value is kept for Get APIs.</summary>
        public const string Hidden = "Hidden";

        public static string Require(string mode)
        {
            if (mode == null)
            {
                throw new ArgumentException(
                    "Display mode must be Control | Text | Hidden (case-sensitive).",
                    nameof(mode));
            }

            switch (mode)
            {
                case Control:
                case Text:
                case Hidden:
                    return mode;
                default:
                    throw new ArgumentException(
                        "Display mode must be Control | Text | Hidden (case-sensitive). Value: '"
                        + mode + "'.",
                        nameof(mode));
            }
        }
    }
}
