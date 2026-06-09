using System;
using System.ComponentModel;
using System.Globalization;

namespace F2B.DesktopApplication.FlaUI
{
    public sealed class MouseButtonTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string text && Enum.TryParse(text, true, out MouseButton button))
                return button;

            return base.ConvertFrom(context, culture, value);
        }
    }
}
