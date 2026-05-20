using System;
using System.Runtime.InteropServices;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>Guards fragile MSHTML COM property reads (AV / RPC_E_*).</summary>
    internal static class ComSafe
    {
        public static string ReadString(Func<string> getter)
        {
            string value;
            TryReadString(getter, out value);
            return value ?? string.Empty;
        }

        public static bool TryReadString(Func<string> getter, out string value)
        {
            value = string.Empty;
            if (getter == null)
                return false;

            try
            {
                value = getter() ?? string.Empty;
                return true;
            }
            catch (AccessViolationException)
            {
                return false;
            }
            catch (COMException)
            {
                return false;
            }
            catch (InvalidComObjectException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
