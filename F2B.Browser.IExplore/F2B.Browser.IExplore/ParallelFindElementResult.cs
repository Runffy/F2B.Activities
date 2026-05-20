using System;

namespace F2B.Browser.IExplore
{
    /// <summary>Result of <see cref="EmbeddedIEWindow.ParallelFindElement"/>.</summary>
    public sealed class ParallelFindElementResult
    {
        public ParallelFindElementResult(int index, IEHtmlElement element)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            Index = index;
            Element = element;
        }

        /// <summary>Zero-based index of the winning locator in the input array.</summary>
        public int Index { get; }

        /// <summary>The first element found among the parallel locators.</summary>
        public IEHtmlElement Element { get; }
    }
}
