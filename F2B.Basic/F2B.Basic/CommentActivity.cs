using System;
using System.Activities;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace F2B.Basic
{
    /// <summary>
    /// Preset comment text colors for the property-grid dropdown.
    /// </summary>
    public enum CommentColor
    {
        Black,
        Red,
        Orange,
        Yellow,
        Green,
        Blue,
        Purple,
        Magenta
    }

    [Designer(typeof(CommentDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("Misc")]
    [DisplayName("Comment")]
    [Description("Design-time comment placeholder on the workflow canvas. Has no runtime effect (skipped).")]
    public sealed class CommentActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public CommentActivity()
        {
            DisplayName = "Comment";
            CommentText = string.Empty;
            Color = CommentColor.Green;
            OtherColor = string.Empty;
        }

        [DisplayName("Comment Text")]
        [Description("Free-form comment shown on the workflow canvas (no field label; edit directly in the textarea).")]
        [Category("Input.A")]
        public string CommentText { get; set; }

        [DisplayName("Color")]
        [Description("Preset text color used when Other Color is empty or invalid. Options: Black, Red, Orange, Yellow, Green, Blue, Purple, Magenta. Default: Green.")]
        [Category("Input.B")]
        [DefaultValue(CommentColor.Green)]
        public CommentColor Color { get; set; }

        [DisplayName("Other Color")]
        [Description("Optional hex color (e.g. #123456). When non-empty and valid, takes priority over Color. Leave empty to use Color.")]
        [Category("Input.B")]
        [DefaultValue("")]
        public string OtherColor { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new CommentActivity
            {
                CommentText = string.Empty,
                Color = CommentColor.Green,
                OtherColor = string.Empty
            };
        }

        protected override void Execute(CodeActivityContext context)
        {
            // Design-time annotation only; no runtime behavior.
        }

        /// <summary>
        /// Resolves the brush used on the canvas: Other Color (valid hex) wins; otherwise Color.
        /// </summary>
        internal static SolidColorBrush ResolveForeground(CommentColor color, string otherColor)
        {
            System.Windows.Media.Color media;
            if (TryParseHexColor(otherColor, out media))
            {
                return new SolidColorBrush(media);
            }

            return new SolidColorBrush(ToMediaColor(color));
        }

        internal static bool TryParseHexColor(string value, out System.Windows.Media.Color color)
        {
            color = default(System.Windows.Media.Color);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string text = value.Trim();
            if (!text.StartsWith("#", StringComparison.Ordinal))
            {
                return false;
            }

            string hex = text.Substring(1);
            try
            {
                if (hex.Length == 6)
                {
                    int rgb = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    color = System.Windows.Media.Color.FromRgb(
                        (byte)((rgb >> 16) & 0xFF),
                        (byte)((rgb >> 8) & 0xFF),
                        (byte)(rgb & 0xFF));
                    return true;
                }

                if (hex.Length == 8)
                {
                    int argb = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    color = System.Windows.Media.Color.FromArgb(
                        (byte)((argb >> 24) & 0xFF),
                        (byte)((argb >> 16) & 0xFF),
                        (byte)((argb >> 8) & 0xFF),
                        (byte)(argb & 0xFF));
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static System.Windows.Media.Color ToMediaColor(CommentColor color)
        {
            switch (color)
            {
                case CommentColor.Black:
                    return Colors.Black;
                case CommentColor.Red:
                    return Colors.Red;
                case CommentColor.Orange:
                    return Colors.Orange;
                case CommentColor.Yellow:
                    return Colors.Yellow;
                case CommentColor.Green:
                    return Colors.Green;
                case CommentColor.Blue:
                    return Colors.Blue;
                case CommentColor.Purple:
                    return Colors.Purple;
                case CommentColor.Magenta:
                    return Colors.Magenta;
                default:
                    return Colors.Green;
            }
        }
    }
}
