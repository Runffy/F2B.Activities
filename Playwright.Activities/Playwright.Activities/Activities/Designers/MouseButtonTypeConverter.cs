using System;
using System.ComponentModel;
using System.Globalization;
using Microsoft.Playwright;

namespace Playwright.Activities
{
    public sealed class MouseButtonTypeConverter : EnumConverter
    {
        public MouseButtonTypeConverter() : base(typeof(MouseButton))
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
                MouseButton.Left,
                MouseButton.Middle,
                MouseButton.Right
            });
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is MouseButton button)
            {
                switch (button)
                {
                    case MouseButton.Left:
                        return "Left";
                    case MouseButton.Middle:
                        return "Middle";
                    case MouseButton.Right:
                        return "Right";
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
                    case "Left":
                        return MouseButton.Left;
                    case "Middle":
                        return MouseButton.Middle;
                    case "Right":
                        return MouseButton.Right;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
