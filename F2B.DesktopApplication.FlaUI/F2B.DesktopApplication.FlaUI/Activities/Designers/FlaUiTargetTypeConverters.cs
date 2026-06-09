using System;
using System.ComponentModel;
using System.Globalization;

namespace F2B.DesktopApplication.FlaUI
{
    public sealed class FlaUiElementTargetTypeConverter : EnumConverter
    {
        public FlaUiElementTargetTypeConverter()
            : base(typeof(ElementTargetType))
        {
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
    }

    public sealed class FlaUiWindowTargetTypeConverter : EnumConverter
    {
        public FlaUiWindowTargetTypeConverter()
            : base(typeof(WindowTargetType))
        {
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is WindowTargetType targetType)
            {
                switch (targetType)
                {
                    case WindowTargetType.Window:
                        return "Window";
                    case WindowTargetType.Selector:
                        return "Selector";
                }
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
