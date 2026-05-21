using System;
using F2B.Browser.IExplore.Com;

namespace F2B.Browser.IExplore
{
    /// <summary>Handle to an MSHTML DOM element returned by <see cref="EmbeddedIEWindow.FindElement"/> / <see cref="EmbeddedIEWindow.FindElements"/>.</summary>
    public sealed class IEHtmlElement
    {
        internal IEHtmlElement(object comElement)
        {
            if (!ComElementHelper.IsValidElement(comElement))
                throw new ArgumentException("Invalid MSHTML element.", nameof(comElement));

            Raw = comElement;
        }

        /// <summary>Underlying COM object (for advanced interop).</summary>
        public object Raw { get; }

        internal static IEHtmlElement From(object comElement)
        {
            if (comElement == null)
                return null;
            return new IEHtmlElement(comElement);
        }

        internal static object Unwrap(IEHtmlElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            return element.Raw;
        }
    }
}
