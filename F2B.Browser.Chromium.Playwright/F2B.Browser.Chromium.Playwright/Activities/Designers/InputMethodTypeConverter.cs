using System;
using System.ComponentModel;
using System.Globalization;

namespace F2B.Browser.Chromium.Playwright
{
    public sealed class InputMethodTypeConverter : EnumConverter
    {
        public InputMethodTypeConverter() : base(typeof(InputMethod))
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
                InputMethod.Fill,
                InputMethod.Type
            });
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is InputMethod method)
            {
                switch (method)
                {
                    case InputMethod.Fill:
                        return "Fill";
                    case InputMethod.Type:
                        return "Type";
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
                    case "Fill":
                        return InputMethod.Fill;
                    case "Type":
                        return InputMethod.Type;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
