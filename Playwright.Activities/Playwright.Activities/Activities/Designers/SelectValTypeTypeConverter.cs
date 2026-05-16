using System;
using System.ComponentModel;
using System.Globalization;

namespace Playwright.Activities
{
    public sealed class SelectValTypeTypeConverter : EnumConverter
    {
        public SelectValTypeTypeConverter() : base(typeof(SelectValType))
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
                SelectValType.Text,
                SelectValType.Value,
                SelectValType.Index
            });
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is SelectValType valType)
            {
                switch (valType)
                {
                    case SelectValType.Text:
                        return "Text";
                    case SelectValType.Value:
                        return "Value";
                    case SelectValType.Index:
                        return "Index";
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
                    case "Text":
                        return SelectValType.Text;
                    case "Value":
                        return SelectValType.Value;
                    case "Index":
                        return SelectValType.Index;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
