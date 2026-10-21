using System;
using System.Activities;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(NotificationDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("Dialog")]
    [DisplayName("Notification")]
    [Description("Shows a transparent toast at the bottom-right of the mouse screen's working area (excludes taskbar). Stacks upward when multiple notifications are visible. Fire-and-forget.")]
    public sealed class NotificationActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public NotificationActivity()
        {
            DisplayName = "Notification";
            FontColor = new InArgument<string>("#FF0000");
            FontSize = new InArgument<float>(30f);
            Bold = true;
            LogMessage = "INFO";
            Timeout = new InArgument<int>(5000);
            MaxWidth = new InArgument<int>(360);
            MaxHeight = new InArgument<int>(200);
        }

        [RequiredArgument]
        [DisplayName("Message")]
        [Description("Text to display on the transparent toast.")]
        [Category("Input.A")]
        public InArgument<string> Message { get; set; }

        [DisplayName("Log Message")]
        [Description("Also write this notification through Log Message at the selected level. INFO / WARN / ERROR, or NONE to skip logging. Default: INFO.")]
        [Category("Input.A")]
        [DefaultValue("INFO")]
        public string LogMessage { get; set; }

        [DisplayName("Font Color")]
        [Description("Text color as #RRGGBB, #AARRGGBB, or a known color name (default #FF0000).")]
        [Category("Input.B")]
        [DefaultValue("#FF0000")]
        public InArgument<string> FontColor { get; set; }

        [DisplayName("Font Size")]
        [Description("Text size in points (default 30).")]
        [Category("Input.B")]
        [DefaultValue(30f)]
        public InArgument<float> FontSize { get; set; }

        [DisplayName("Bold")]
        [Description("Whether the toast text is bold (default true).")]
        [Category("Input.B")]
        [DefaultValue(true)]
        public bool Bold { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("How long the toast stays fully visible before fade-out (milliseconds). Use 0 to keep it until the process exits.")]
        [Category("Input.Z")]
        [DefaultValue(5000)]
        public InArgument<int> Timeout { get; set; }

        [DisplayName("Max Width")]
        [Description("Maximum toast width in pixels. Longer text wraps. Window shrinks to fit content when smaller.")]
        [Category("Input.C")]
        [DefaultValue(360)]
        public InArgument<int> MaxWidth { get; set; }

        [DisplayName("Max Height")]
        [Description("Maximum toast height in pixels. Overflowing text is truncated with an ellipsis.")]
        [Category("Input.C")]
        [DefaultValue(200)]
        public InArgument<int> MaxHeight { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new NotificationActivity
            {
                FontColor = new InArgument<string>("#FF0000"),
                FontSize = new InArgument<float>(30f),
                Bold = true,
                LogMessage = "INFO",
                Timeout = new InArgument<int>(5000),
                MaxWidth = new InArgument<int>(360),
                MaxHeight = new InArgument<int>(200)
            };
        }

        protected override void Execute(CodeActivityContext context)
        {
            string message = Message == null ? null : Message.Get(context);
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Message is required.", nameof(Message));
            }

            string colorText = FontColor == null ? null : FontColor.Get(context);
            Color color = ParseColor(colorText, Color.Red);

            float fontSize = FontSize == null ? 30f : FontSize.Get(context);
            if (fontSize <= 0)
            {
                fontSize = 30f;
            }

            int timeoutMs = Timeout == null ? 5000 : Timeout.Get(context);
            if (timeoutMs < 0)
            {
                timeoutMs = 0;
            }

            int maxWidth = MaxWidth == null ? 360 : MaxWidth.Get(context);
            if (maxWidth <= 0)
            {
                maxWidth = 360;
            }

            int maxHeight = MaxHeight == null ? 200 : MaxHeight.Get(context);
            if (maxHeight <= 0)
            {
                maxHeight = 200;
            }

            string logLevel = NormalizeLogLevel(LogMessage);
            if (!string.Equals(logLevel, "NONE", StringComparison.Ordinal))
            {
                LogMessageActivity.WriteFormatted(
                    context.WorkflowInstanceId.ToString(),
                    logLevel,
                    message);
            }

            NotificationToastService.Show(message, color, fontSize, Bold, timeoutMs, maxWidth, maxHeight);
        }

        private static string NormalizeLogLevel(string value)
        {
            string level = (value ?? "INFO").Trim().ToUpperInvariant();
            if (level == "INFO" || level == "WARN" || level == "ERROR" || level == "NONE")
            {
                return level;
            }

            return "INFO";
        }

        private static Color ParseColor(string value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string text = value.Trim();
            try
            {
                if (text.StartsWith("#", StringComparison.Ordinal))
                {
                    string hex = text.Substring(1);
                    if (hex.Length == 6)
                    {
                        int rgb = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
                    }

                    if (hex.Length == 8)
                    {
                        int argb = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        return Color.FromArgb(argb);
                    }
                }

                Color named = Color.FromName(text);
                if (named.IsKnownColor || named.A > 0 || string.Equals(text, "Transparent", StringComparison.OrdinalIgnoreCase))
                {
                    // FromName returns a valid ARGB for known names; unknown names get A=0 RGB=0.
                    if (named.A == 0 && named.R == 0 && named.G == 0 && named.B == 0
                        && !string.Equals(text, "Black", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(text, "Transparent", StringComparison.OrdinalIgnoreCase))
                    {
                        return fallback;
                    }

                    return named;
                }
            }
            catch
            {
            }

            return fallback;
        }
    }
}
