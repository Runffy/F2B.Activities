using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace F2B.Basic
{
    /// <summary>
    /// Supplies a fixed HTTP verb dropdown list in the property grid (Method is not shown on the activity canvas).
    /// </summary>
    public sealed class HttpVerbChoiceConverter : TypeConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return false;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            IReadOnlyList<string> verbs = HttpRequestActivity.GetAllowedHttpMethods();
            var wrapped = new object[verbs.Count];
            for (var i = 0; i < verbs.Count; i++)
            {
                wrapped[i] = verbs[i];
            }

            return new StandardValuesCollection(wrapped);
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string s)
            {
                return s;
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
