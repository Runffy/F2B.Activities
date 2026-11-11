using System;

namespace F2B.Forms.Model
{
    /// <summary>
    /// MixTable column types. Names match Designer control type names exactly
    /// (case-sensitive). No abbreviations / aliases.
    /// Link is MixTable-only (no standalone Designer control).
    /// </summary>
    public static class MixTableColumnType
    {
        public const string Label = FormControlType.Label;
        public const string TextBox = FormControlType.TextBox;
        public const string ComboBox = FormControlType.ComboBox;
        public const string CheckBox = FormControlType.CheckBox;
        public const string Button = FormControlType.Button;
        /// <summary>Hyperlink column (MixTable-only; not a Designer toolbox control).</summary>
        public const string Link = "Link";

        public static bool IsKnown(string type)
        {
            if (type == null)
            {
                return false;
            }

            switch (type)
            {
                case Label:
                case TextBox:
                case ComboBox:
                case CheckBox:
                case Button:
                case Link:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns <paramref name="type"/> unchanged when it is an exact known name;
        /// otherwise throws. Whitespace-only / null is rejected.
        /// </summary>
        public static string Require(string type)
        {
            if (type == null || !IsKnown(type))
            {
                throw new ArgumentException(
                    "MixTable column type must be exactly one of: "
                    + Label + ", " + TextBox + ", " + ComboBox + ", "
                    + CheckBox + ", " + Button + ", " + Link
                    + " (case-sensitive, no aliases). Value: '"
                    + (type ?? "(null)") + "'.",
                    nameof(type));
            }

            return type;
        }
    }
}
