using System;
using System.ComponentModel;
using System.Globalization;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeSwitchTabByTypeConverter : EnumConverter
    {
        public BridgeSwitchTabByTypeConverter() : base(typeof(BridgeSwitchTabByType))
        {
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is BridgeSwitchTabByType item)
            {
                switch (item)
                {
                    case BridgeSwitchTabByType.Index: return "Index";
                    case BridgeSwitchTabByType.Title: return "Title";
                    case BridgeSwitchTabByType.TitleRegex: return "Title Regex";
                    case BridgeSwitchTabByType.Url: return "Url";
                    case BridgeSwitchTabByType.UrlRegex: return "Url Regex";
                    case BridgeSwitchTabByType.Tab: return "Tab";
                }
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string text)
            {
                switch (text.Trim())
                {
                    case "Index": return BridgeSwitchTabByType.Index;
                    case "Title": return BridgeSwitchTabByType.Title;
                    case "Title Regex": return BridgeSwitchTabByType.TitleRegex;
                    case "Url": return BridgeSwitchTabByType.Url;
                    case "Url Regex": return BridgeSwitchTabByType.UrlRegex;
                    case "Tab": return BridgeSwitchTabByType.Tab;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }

    public sealed class BridgeSelectValTypeConverter : EnumConverter
    {
        public BridgeSelectValTypeConverter() : base(typeof(BridgeSelectValType))
        {
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is BridgeSelectValType item)
            {
                switch (item)
                {
                    case BridgeSelectValType.Text: return "Text";
                    case BridgeSelectValType.Value: return "Value";
                    case BridgeSelectValType.Index: return "Index";
                }
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string text)
            {
                switch (text.Trim())
                {
                    case "Text": return BridgeSelectValType.Text;
                    case "Value": return BridgeSelectValType.Value;
                    case "Index": return BridgeSelectValType.Index;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
