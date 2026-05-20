using System;

namespace F2B.Browser.IExplore.Com
{
    internal static class ComElementHelper
    {
        public static bool IsValidElement(object value)
        {
            if (value == null)
                return false;
            if (value is DBNull)
                return false;
            return true;
        }
    }
}
