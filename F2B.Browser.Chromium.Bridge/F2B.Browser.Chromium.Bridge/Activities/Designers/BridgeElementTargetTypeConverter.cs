using System;
using System.ComponentModel;
using System.Globalization;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeElementTargetTypeConverter : EnumConverter
    {
        public BridgeElementTargetTypeConverter() : base(typeof(BridgeElementTargetType))
        {
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            return new StandardValuesCollection(new object[]
            {
                BridgeElementTargetType.Element,
                BridgeElementTargetType.Selector
            });
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is BridgeElementTargetType targetType)
            {
                switch (targetType)
                {
                    case BridgeElementTargetType.Element:
                        return "Element";
                    case BridgeElementTargetType.Selector:
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
                        return BridgeElementTargetType.Element;
                    case "Selector":
                        return BridgeElementTargetType.Selector;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
