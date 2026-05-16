using System;
using System.ComponentModel;
using System.Globalization;

namespace Playwright.Activities
{
    public sealed class BrowserSwitchTabByTypeTypeConverter : EnumConverter
    {
        public BrowserSwitchTabByTypeTypeConverter() : base(typeof(BrowserSwitchTabByType))
        {
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return true;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            return new StandardValuesCollection(new object[]
            {
                BrowserSwitchTabByType.Index,
                BrowserSwitchTabByType.Title,
                BrowserSwitchTabByType.TitleRegex,
                BrowserSwitchTabByType.Url,
                BrowserSwitchTabByType.UrlRegex,
                BrowserSwitchTabByType.Tab
            });
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is BrowserSwitchTabByType byType)
            {
                switch (byType)
                {
                    case BrowserSwitchTabByType.Index:
                        return "Index";
                    case BrowserSwitchTabByType.Title:
                        return "Title";
                    case BrowserSwitchTabByType.TitleRegex:
                        return "Title Regex";
                    case BrowserSwitchTabByType.Url:
                        return "Url";
                    case BrowserSwitchTabByType.UrlRegex:
                        return "Url Regex";
                    case BrowserSwitchTabByType.Tab:
                        return "Tab";
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
                    case "Index":
                        return BrowserSwitchTabByType.Index;
                    case "Title":
                        return BrowserSwitchTabByType.Title;
                    case "Title Regex":
                        return BrowserSwitchTabByType.TitleRegex;
                    case "Url":
                        return BrowserSwitchTabByType.Url;
                    case "Url Regex":
                        return BrowserSwitchTabByType.UrlRegex;
                    case "Tab":
                        return BrowserSwitchTabByType.Tab;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
