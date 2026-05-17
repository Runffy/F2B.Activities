using System;
using System.ComponentModel;
using System.Globalization;

namespace F2B.Browser.Chromium.Playwright
{
    public sealed class ElementTargetTypeConverter : EnumConverter
    {
        public ElementTargetTypeConverter() : base(typeof(ElementTargetType))
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
                ElementTargetType.Element,
                ElementTargetType.Selector
            });
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is ElementTargetType targetType)
            {
                switch (targetType)
                {
                    case ElementTargetType.Element:
                        return "Element";
                    case ElementTargetType.Selector:
                        return "Selector";
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
                    case "Element":
                        return ElementTargetType.Element;
                    case "Selector":
                        return ElementTargetType.Selector;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
