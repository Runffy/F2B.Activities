using System;
using System.IO;
using System.Text;

namespace F2B.Basic
{
    /// <summary>
    /// File-name helpers for OpenRPA expressions and activities.
    /// Expression: <c>F2B.Basic.FileName.ConvertFileNameString(in_string, replace_string)</c>
    /// </summary>
    public static class FileName
    {
        /// <summary>
        /// Replaces characters that are illegal in a Windows file name
        /// (<see cref="Path.GetInvalidFileNameChars"/>) with <paramref name="replaceString"/>.
        /// </summary>
        /// <param name="inString">Candidate file name (not a full path).</param>
        /// <param name="replaceString">
        /// Replacement for each illegal character. Must not itself contain illegal file-name characters.
        /// Use empty string to strip illegal characters. Default when called from the activity is "_".
        /// </param>
        public static string ConvertFileNameString(string inString, string replaceString)
        {
            if (replaceString == null)
            {
                replaceString = string.Empty;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            if (replaceString.IndexOfAny(invalid) >= 0)
            {
                throw new ArgumentException(
                    "replace_string must not contain characters illegal in a Windows file name.",
                    nameof(replaceString));
            }

            if (string.IsNullOrEmpty(inString))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(inString.Length);
            for (int i = 0; i < inString.Length; i++)
            {
                char ch = inString[i];
                bool illegal = false;
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (ch == invalid[j])
                    {
                        illegal = true;
                        break;
                    }
                }

                if (illegal)
                {
                    sb.Append(replaceString);
                }
                else
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }
    }
}
