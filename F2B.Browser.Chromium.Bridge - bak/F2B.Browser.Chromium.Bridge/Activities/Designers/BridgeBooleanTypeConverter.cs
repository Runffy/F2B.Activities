using System;
using System.ComponentModel;
using System.Globalization;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeBooleanTypeConverter : TypeConverter
    {
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            return new StandardValuesCollection(new object[] { true, false });
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string text)
            {
                switch (text.Trim())
                {
                    case "True":
                        return true;
                    case "False":
                        return false;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is bool b)
                return b ? "True" : "False";

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
