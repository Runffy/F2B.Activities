using System;
using System.Collections.Generic;
using System.Text;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpErrorFormatter
    {
        public static string Format(object errorValue)
        {
            if (errorValue == null)
            {
                return "unknown error";
            }

            var dict = errorValue as Dictionary<string, object>;
            if (dict == null)
            {
                return Convert.ToString(errorValue) ?? "unknown error";
            }

            var message = CdpValueConverter.GetString(dict, "message");
            var code = dict.ContainsKey("code") ? CdpValueConverter.GetString(dict, "code") : null;
            var data = CdpValueConverter.GetString(dict, "data");

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(message))
            {
                sb.Append(message);
            }
            else
            {
                sb.Append("unknown error");
            }

            if (!string.IsNullOrEmpty(code))
            {
                sb.Append(" (code ").Append(code).Append(")");
            }

            if (!string.IsNullOrEmpty(data))
            {
                sb.Append(": ").Append(data);
            }

            return sb.ToString();
        }

        public static string FormatExceptionDetails(object exceptionDetails)
        {
            if (exceptionDetails == null)
            {
                return "unknown exception";
            }

            var dict = exceptionDetails as Dictionary<string, object>;
            if (dict == null)
            {
                return Convert.ToString(exceptionDetails) ?? "unknown exception";
            }

            var text = CdpValueConverter.GetString(dict, "text");
            var exception = CdpValueConverter.GetDictionary(dict, "exception");
            var description = exception != null ? CdpValueConverter.GetString(exception, "description") : null;
            var value = exception != null ? CdpValueConverter.GetString(exception, "value") : null;

            if (!string.IsNullOrEmpty(description))
            {
                return description;
            }

            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            return Format(exceptionDetails);
        }
    }
}
