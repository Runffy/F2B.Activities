using System;
using System.ComponentModel;
using System.Globalization;

namespace Playwright.Activities
{
    public sealed class ClickValidateModeTypeConverter : EnumConverter
    {
        public ClickValidateModeTypeConverter() : base(typeof(ClickValidateMode))
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
                ClickValidateMode.None,
                ClickValidateMode.ElementDisappear,
                ClickValidateMode.ElementAppear
            });
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is ClickValidateMode mode)
            {
                switch (mode)
                {
                    case ClickValidateMode.None:
                        return "None";
                    case ClickValidateMode.ElementDisappear:
                        return "ElementDisappear";
                    case ClickValidateMode.ElementAppear:
                        return "ElementAppear";
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
                    case "None":
                        return ClickValidateMode.None;
                    case "ElementDisappear":
                    case "CurrentElementDisappear":
                        return ClickValidateMode.ElementDisappear;
                    case "ElementAppear":
                    case "SelectorAppear":
                        return ClickValidateMode.ElementAppear;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
