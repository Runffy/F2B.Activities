using System;
using System.ComponentModel;
using System.Globalization;

namespace F2B.Terminal.PCOMM
{
    public sealed class PcommKeyTypeConverter : EnumConverter
    {
        public PcommKeyTypeConverter()
            : base(typeof(PcommKey))
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
                PcommKey.Enter,
                PcommKey.Tab,
                PcommKey.Backspace,
                PcommKey.F1,
                PcommKey.F2,
                PcommKey.F3,
                PcommKey.F4,
                PcommKey.F5,
                PcommKey.F6,
                PcommKey.F7,
                PcommKey.F8,
                PcommKey.F9,
                PcommKey.F10,
                PcommKey.F11,
                PcommKey.F12
            });
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is PcommKey key)
            {
                return PcommKeyHelper.ToDisplayName(key);
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string text && PcommKeyHelper.TryParseDisplayName(text, out var key))
            {
                return key;
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
