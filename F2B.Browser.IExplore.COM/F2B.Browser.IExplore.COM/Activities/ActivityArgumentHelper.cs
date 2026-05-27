using System;
using System.Activities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;

namespace F2B.Browser.IExplore.COM
{
    internal static class ActivityArgumentHelper
    {
        private static readonly JavaScriptSerializer JsonSerializer = new JavaScriptSerializer();

        internal static T GetOrDefault<T>(InArgument<T> argument, CodeActivityContext context, T defaultValue)
        {
            if (argument == null)
                return defaultValue;

            var value = argument.Get(context);
            return value == null ? defaultValue : value;
        }

        internal static void ApplyDelayBefore(InArgument<int> delayBefore, CodeActivityContext context, int defaultValue = 300)
        {
            var delayMs = GetOrDefault(delayBefore, context, defaultValue);
            if (delayMs <= 0)
                return;

            Thread.Sleep(delayMs);
        }

        internal static IDictionary<string, object> ParseJsonObject(string json, string argumentName, bool required)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                if (required)
                    throw new ArgumentException(argumentName + " is required.");

                return null;
            }

            object parsed;
            try
            {
                parsed = JsonSerializer.DeserializeObject(json);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(argumentName + " must be a valid JSON object string.", ex);
            }

            var dictionary = parsed as IDictionary<string, object>;
            if (dictionary == null)
                throw new ArgumentException(argumentName + " must be a JSON object.");

            return dictionary;
        }

        internal static IList<object> ParseJsonArray(string json, string argumentName, bool required)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                if (required)
                    throw new ArgumentException(argumentName + " is required.");

                return null;
            }

            object parsed;
            try
            {
                parsed = JsonSerializer.DeserializeObject(json);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(argumentName + " must be a valid JSON array string.", ex);
            }

            var array = parsed as object[];
            if (array != null)
                return array.ToList();

            var enumerable = parsed as IEnumerable;
            if (enumerable != null && !(parsed is string) && !(parsed is IDictionary))
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                    list.Add(item);

                return list;
            }

            throw new ArgumentException(argumentName + " must be a JSON array.");
        }
    }
}
