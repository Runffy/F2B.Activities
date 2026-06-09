using System.Collections.Generic;
using F2B.Browser.IExplore.COM;

namespace IE.Inspector.Services
{
    public sealed class IeBrowserContext
    {
        public IEWindowController Controller { get; set; }
        public IList<object> FramePath { get; set; } = new List<object>();
    }
}
